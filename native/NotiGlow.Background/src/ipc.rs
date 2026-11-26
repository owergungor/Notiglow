use std::collections::VecDeque;
use std::fs;
use std::path::Path;
use std::process::Command;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::thread;
use std::time::{Duration, Instant};

use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use windows::core::{PCWSTR, PWSTR};
use windows::Win32::Foundation::{
    CloseHandle, GetLastError, LocalFree, HANDLE, HLOCAL, HWND, INVALID_HANDLE_VALUE, LPARAM,
    WPARAM,
};
use windows::Win32::Security::Authorization::{
    ConvertSidToStringSidW, ConvertStringSecurityDescriptorToSecurityDescriptorW, SDDL_REVISION_1,
};
use windows::Win32::Security::{
    GetTokenInformation, TokenUser, PSECURITY_DESCRIPTOR, SECURITY_ATTRIBUTES, TOKEN_QUERY,
    TOKEN_USER,
};
use windows::Win32::Storage::FileSystem::{
    FlushFileBuffers, ReadFile, WriteFile, PIPE_ACCESS_DUPLEX,
};
use windows::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, DisconnectNamedPipe, GetNamedPipeClientProcessId,
    PIPE_READMODE_BYTE, PIPE_REJECT_REMOTE_CLIENTS, PIPE_TYPE_BYTE, PIPE_WAIT,
};
use windows::Win32::System::RemoteDesktop::ProcessIdToSessionId;
use windows::Win32::System::Threading::{
    GetCurrentProcess, GetCurrentProcessId, OpenProcessToken,
};
use windows::Win32::UI::WindowsAndMessaging::{
    AllowSetForegroundWindow, FindWindowW, PostMessageW, RegisterWindowMessageW,
     SetForegroundWindow, ShowWindow, ASFW_ANY, HWND_BROADCAST, SW_RESTORE,
};

use crate::games::{ActiveGameInfo, GameDetectorState};
use crate::glow::{enumerate_monitors, GlowEngine};
use crate::models::{AppProfile, AppSettings, BurstMode, GameGlowOverride, MonitorMode};
use crate::notifications::{push_notification_event, NotificationEventRecord};

pub const PROTOCOL_VERSION: u32 = 1;
pub const SCHEMA_VERSION: u32 = 1;
pub const WORKER_VERSION: &str = env!("CARGO_PKG_VERSION");
pub const MAX_MESSAGE_BYTES: usize = 65_536; // 64 KB hard cap

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrayActionRecord {
    pub seq: u64,
    pub action: String,
    pub value: Option<bool>,
}

#[derive(Debug, Clone)]
pub struct ConfigSnapshot {
    pub settings: AppSettings,
    pub profiles: Vec<AppProfile>,
    pub revision: u64,
    pub client_revision: u64,
    pub schema_version: u32,
}

impl Default for ConfigSnapshot {
    fn default() -> Self {
        Self {
            settings: AppSettings::default(),
            profiles: Vec::new(),
            revision: 0,
            client_revision: 0,
            schema_version: SCHEMA_VERSION,
        }
    }
}

pub struct WorkerState {
    pub start_time: Instant,
    pub running: &'static AtomicBool,
    pub shutting_down: AtomicBool,
    pub ipc_only: bool,
    pub config_dir: Option<String>,
    pub ui_exe_path: Option<String>,
    pub reload_lock: Mutex<()>,
    pub config: RwLock<ConfigSnapshot>,
    pub glow_engine: Arc<GlowEngine>,
    pub game_detector: Arc<GameDetectorState>,
    pub tray_active: &'static AtomicBool,
    pub taskbar_restores: &'static AtomicU64,
    pub notif_access: &'static AtomicU64,
    pub notif_polls: &'static AtomicU64,
    pub notif_new: &'static AtomicU64,
    pub notif_denied_checks: &'static AtomicU64,
    pub notif_history: AtomicU64,
    pub notif_removed: AtomicU64,
    pub notif_duplicates_suppressed: AtomicU64,
    pub notif_event_mode: AtomicBool,
    pub notif_event_signaled: AtomicBool,
    pub active_listener_count: AtomicU64,
    pub notif_access_transitions: AtomicU64,
    pub test_access_override: AtomicU64,
    pub next_notif_seq: AtomicU64,
    pub notif_events: Mutex<VecDeque<NotificationEventRecord>>,
    pub next_tray_seq: AtomicU64,
    pub tray_actions: Mutex<VecDeque<TrayActionRecord>>,
    pub pending_master_enabled: Mutex<Option<bool>>,
    pub pending_gaming_mode: Mutex<Option<bool>>,
    pub pending_ui_sync: AtomicBool,
    pub last_ui_open_ms: AtomicU64,
    pub simulate_taskbar_cb: fn(),
    pub request_shutdown_cb: fn(&str),
    pub log_cb: fn(&str),
}

pub struct ReloadOutcome {
    pub reloaded: bool,
    pub stale_ignored: bool,
    pub profiles_count: usize,
    pub tracked_count: usize,
    pub settings_revision: u64,
    pub client_revision: u64,
    pub schema_version: u32,
}

impl WorkerState {
    pub fn effective_master_enabled(&self) -> bool {
        if let Ok(g) = self.pending_master_enabled.lock() {
            if let Some(v) = *g {
                return v;
            }
        }
        self.config
            .read()
            .map(|c| c.settings.MasterEnabled)
            .unwrap_or(true)
    }

    pub fn effective_gaming_mode(&self) -> bool {
        if let Ok(g) = self.pending_gaming_mode.lock() {
            if let Some(v) = *g {
                return v;
            }
        }
        self.config
            .read()
            .map(|c| c.settings.GamingModeEnabled)
            .unwrap_or(false)
    }

    pub fn set_master_override(&self, enabled: bool, mark_pending: bool) {
        if let Ok(mut g) = self.pending_master_enabled.lock() {
            *g = Some(enabled);
        }
        if mark_pending {
            self.pending_ui_sync.store(true, Ordering::SeqCst);
        }
        self.push_tray_action("MasterToggled", Some(enabled));
    }

    pub fn set_gaming_override(&self, enabled: bool, mark_pending: bool) {
        if let Ok(mut g) = self.pending_gaming_mode.lock() {
            *g = Some(enabled);
        }
        if mark_pending {
            self.pending_ui_sync.store(true, Ordering::SeqCst);
        }
        self.push_tray_action("GamingToggled", Some(enabled));
    }

    pub fn push_tray_action(&self, action: &str, value: Option<bool>) -> u64 {
        let seq = self.next_tray_seq.fetch_add(1, Ordering::SeqCst) + 1;
        if let Ok(mut q) = self.tray_actions.lock() {
            if q.len() >= 128 {
                q.pop_front();
            }
            q.push_back(TrayActionRecord {
                seq,
                action: action.to_string(),
                value,
            });
        }
        seq
    }

    /// Signals an existing NotiGlow UI window or spawns NotiGlow.exe if closed (debounced 250ms).
    pub fn open_or_signal_ui(&self, nav_tag: Option<&str>, debounce: bool) -> (bool, bool, Option<u32>) {
        let now_ms = self.start_time.elapsed().as_millis() as u64 + 1000;
        if debounce {
            let prev = self.last_ui_open_ms.load(Ordering::Relaxed);
            if now_ms.saturating_sub(prev) < 250 {
                return (false, false, None);
            }
        }
        self.last_ui_open_ms.store(now_ms, Ordering::Relaxed);

        unsafe {
            let wm_name = to_wide_null("WM_SHOW_NOTIGLOW");
            let wm_show = RegisterWindowMessageW(PCWSTR(wm_name.as_ptr()));
            let title = to_wide_null("NotiGlow");
            let existing_hwnd = FindWindowW(PCWSTR::null(), PCWSTR(title.as_ptr()))
                .unwrap_or(HWND(std::ptr::null_mut()));

            if !existing_hwnd.0.is_null() {
                let _ = AllowSetForegroundWindow(ASFW_ANY);
                let _ = ShowWindow(existing_hwnd, SW_RESTORE);
                let _ = SetForegroundWindow(existing_hwnd);
                if wm_show != 0 {
                    let _ = PostMessageW(existing_hwnd, wm_show, WPARAM(0), LPARAM(0));
                    let _ = PostMessageW(HWND_BROADCAST, wm_show, WPARAM(0), LPARAM(0));
                }
                if let Some(tag) = nav_tag {
                    if tag.eq_ignore_ascii_case("Advanced") {
                        self.push_tray_action("OpenUpdates", None);
                    } else {
                        self.push_tray_action("OpenUi", None);
                    }
                } else {
                    self.push_tray_action("OpenUi", None);
                }
                return (false, true, None);
            }
        }

        // UI window not found -> spawn NotiGlow.exe if configured and present on disk
        if let Some(ref exe) = self.ui_exe_path {
            let p = Path::new(exe);
            if p.exists() {
                let mut cmd = Command::new(p);
                if let Some(dir) = p.parent() {
                    cmd.current_dir(dir);
                }
                if let Some(ref cfg_dir) = self.config_dir {
                    cmd.env("NOTIGLOW_CONFIG_DIR", cfg_dir);
                }
                if let Some(tag) = nav_tag {
                    if tag.eq_ignore_ascii_case("Advanced") {
                        self.push_tray_action("OpenUpdates", None);
                    }
                }
                if let Ok(child) = cmd.spawn() {
                    let pid = child.id();
                    (self.log_cb)(&format!("Launched NotiGlow UI process (PID={})", pid));
                    return (true, false, Some(pid));
                }
            }
        }

        (false, false, None)
    }

    fn read_and_parse_files(
        base_dir: &str,
    ) -> Result<(AppSettings, Vec<AppProfile>), (&'static str, String)> {
        let settings_path = Path::new(base_dir).join("settings.json");
        let profiles_path = Path::new(base_dir).join("profiles.json");

        let settings = if settings_path.exists() {
            let raw = fs::read_to_string(&settings_path)
                .map_err(|e| ("ERR_CONFIG_IO", format!("Read settings failed: {}", e)))?;
            serde_json::from_str::<AppSettings>(raw.trim_start_matches('\u{feff}'))
                .map_err(|e| ("ERR_CONFIG_PARSE", format!("Parse settings failed: {}", e)))?
        } else {
            AppSettings::default()
        };

        let profiles = if profiles_path.exists() {
            let raw = fs::read_to_string(&profiles_path)
                .map_err(|e| ("ERR_CONFIG_IO", format!("Read profiles failed: {}", e)))?;
            serde_json::from_str::<Vec<AppProfile>>(raw.trim_start_matches('\u{feff}'))
                .map_err(|e| ("ERR_CONFIG_PARSE", format!("Parse profiles failed: {}", e)))?
        } else {
            Vec::new()
        };

        Ok((settings, profiles))
    }

    pub fn reload_config_from_disk(
        &self,
        incoming_client_rev: Option<u64>,
        clear_pending_sync: bool,
    ) -> Result<ReloadOutcome, (&'static str, String)> {
        let _reload_guard = self
            .reload_lock
            .lock()
            .map_err(|_| ("ERR_INTERNAL", "Reload lock poisoned".to_string()))?;

        if let Some(req_rev) = incoming_client_rev {
            if let Ok(cur) = self.config.read() {
                if req_rev > 0 && req_rev < cur.client_revision {
                    return Ok(ReloadOutcome {
                        reloaded: false,
                        stale_ignored: true,
                        profiles_count: cur.profiles.len(),
                        tracked_count: cur.settings.TrackedGames.len(),
                        settings_revision: cur.revision,
                        client_revision: cur.client_revision,
                        schema_version: cur.schema_version,
                    });
                }
            }
        }

        let base_dir = match &self.config_dir {
            Some(d) => d.clone(),
            None => match std::env::var("APPDATA") {
                Ok(ap) => Path::new(&ap)
                    .join("NotiGlow")
                    .to_string_lossy()
                    .to_string(),
                Err(_) => return Err(("ERR_CONFIG_IO", "APPDATA not set".to_string())),
            },
        };

        let (settings, profiles) = match Self::read_and_parse_files(&base_dir) {
            Ok(pair) => pair,
            Err(_) => {
                thread::sleep(Duration::from_millis(25));
                Self::read_and_parse_files(&base_dir)?
            }
        };

        let profiles_count = profiles.len();
        let tracked_count = settings.TrackedGames.len();

        let mut guard = self
            .config
            .write()
            .map_err(|_| ("ERR_INTERNAL", "Config lock poisoned".to_string()))?;
        let next_rev = guard.revision + 1;
        let next_client_rev = match incoming_client_rev {
            Some(r) if r > guard.client_revision => r,
            _ => guard.client_revision,
        };
        *guard = ConfigSnapshot {
            settings,
            profiles,
            revision: next_rev,
            client_revision: next_client_rev,
            schema_version: SCHEMA_VERSION,
        };

        if clear_pending_sync {
            if let Ok(mut pm) = self.pending_master_enabled.lock() {
                *pm = None;
            }
            if let Ok(mut pg) = self.pending_gaming_mode.lock() {
                *pg = None;
            }
            self.pending_ui_sync.store(false, Ordering::SeqCst);
        }

        Ok(ReloadOutcome {
            reloaded: true,
            stale_ignored: false,
            profiles_count,
            tracked_count,
            settings_revision: next_rev,
            client_revision: next_client_rev,
            schema_version: SCHEMA_VERSION,
        })
    }
}

#[derive(Debug, Deserialize)]
struct IpcRequest {
    v: u32,
    #[serde(default)]
    id: String,
    cmd: String,
    #[serde(default)]
    payload: Value,
}

#[derive(Debug, Serialize)]
struct IpcError {
    code: String,
    message: String,
}

#[derive(Debug, Serialize)]
struct IpcResponse {
    v: u32,
    id: String,
    ok: bool,
    data: Option<Value>,
    error: Option<IpcError>,
}

impl IpcResponse {
    fn ok(id: String, data: Value) -> Self {
        Self {
            v: PROTOCOL_VERSION,
            id,
            ok: true,
            data: Some(data),
            error: None,
        }
    }

    fn err(id: String, code: &str, message: impl Into<String>) -> Self {
        Self {
            v: PROTOCOL_VERSION,
            id,
            ok: false,
            data: None,
            error: Some(IpcError {
                code: code.to_string(),
                message: message.into(),
            }),
        }
    }

    fn to_ndjson(&self) -> String {
        let mut s = serde_json::to_string(self).unwrap_or_else(|_| {
            r#"{"v":1,"id":"","ok":false,"data":null,"error":{"code":"ERR_INTERNAL","message":"serialize failed"}}"#
                .to_string()
        });
        s.push('\n');
        s
    }
}

#[derive(Debug, Deserialize)]
struct PlayGlowArgs {
    #[serde(default)]
    profile: AppProfile,
    settings: Option<AppSettings>,
    #[serde(default)]
    is_game_running: bool,
    active_game_id: Option<String>,
    #[serde(default)]
    is_test: bool,
}

fn to_wide_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

pub fn get_current_session_id() -> u32 {
    unsafe {
        let pid = GetCurrentProcessId();
        let mut sid = 0u32;
        if ProcessIdToSessionId(pid, &mut sid).is_ok() {
            sid
        } else {
            0
        }
    }
}

pub fn default_pipe_name() -> String {
    format!(r"\\.\pipe\NotiGlow_IPC_v1_{}", get_current_session_id())
}

unsafe fn build_current_user_sd() -> Option<PSECURITY_DESCRIPTOR> {
    let mut token = HANDLE::default();
    if OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut token).is_err() {
        return None;
    }

    let mut needed = 0u32;
    let _ = GetTokenInformation(token, TokenUser, None, 0, &mut needed);
    if needed == 0 {
        let _ = CloseHandle(token);
        return None;
    }

    let mut buf = vec![0u8; needed as usize];
    let ok = GetTokenInformation(
        token,
        TokenUser,
        Some(buf.as_mut_ptr() as *mut _),
        needed,
        &mut needed,
    );
    let _ = CloseHandle(token);
    if ok.is_err() {
        return None;
    }

    let token_user = &*(buf.as_ptr() as *const TOKEN_USER);
    let mut sid_pwstr = PWSTR::null();
    if ConvertSidToStringSidW(token_user.User.Sid, &mut sid_pwstr).is_err() || sid_pwstr.is_null() {
        return None;
    }

    let sid_str = sid_pwstr.to_string().unwrap_or_default();
    let _ = LocalFree(HLOCAL(sid_pwstr.0 as *mut _));
    if sid_str.is_empty() {
        return None;
    }

    let sddl = to_wide_null(&format!("D:(A;;GA;;;{})", sid_str));
    let mut sd = PSECURITY_DESCRIPTOR::default();
    if ConvertStringSecurityDescriptorToSecurityDescriptorW(
        PCWSTR(sddl.as_ptr()),
        SDDL_REVISION_1,
        &mut sd,
        None,
    )
    .is_ok()
    {
        Some(sd)
    } else {
        None
    }
}

unsafe fn verify_client_same_session(h_pipe: HANDLE) -> bool {
    let mut client_pid = 0u32;
    if GetNamedPipeClientProcessId(h_pipe, &mut client_pid).is_err() {
        return true;
    }
    let mut client_sid = 0u32;
    if ProcessIdToSessionId(client_pid, &mut client_sid).is_err() {
        return false;
    }
    client_sid == get_current_session_id()
}

unsafe fn read_message_bounded(h_pipe: HANDLE) -> Result<String, &'static str> {
    let mut acc: Vec<u8> = Vec::with_capacity(512);
    let mut chunk = [0u8; 4096];

    loop {
        let mut bytes_read = 0u32;
        let res = ReadFile(h_pipe, Some(&mut chunk), Some(&mut bytes_read), None);
        if res.is_err() || bytes_read == 0 {
            if acc.is_empty() {
                return Err("ERR_EOF");
            }
            break;
        }

        if acc.len() + (bytes_read as usize) > MAX_MESSAGE_BYTES {
            return Err("ERR_MESSAGE_TOO_LARGE");
        }

        let slice = &chunk[..bytes_read as usize];
        if let Some(nl_pos) = slice.iter().position(|&b| b == b'\n') {
            acc.extend_from_slice(&slice[..nl_pos]);
            break;
        }
        acc.extend_from_slice(slice);

        if bytes_read < chunk.len() as u32 && acc.ends_with(b"}") {
            break;
        }
    }

    String::from_utf8(acc)
        .map(|s| s.trim().to_string())
        .map_err(|_| "ERR_BAD_UTF8")
}

pub fn dispatch_json_request(raw: &str, state: &Arc<WorkerState>) -> (String, bool) {
    if raw.starts_with("v1|") {
        let parts: Vec<&str> = raw.split('|').collect();
        let cmd = parts.get(1).copied().unwrap_or("");
        let synth = json!({ "v": 1, "id": "legacy", "cmd": cmd, "payload": {} }).to_string();
        return dispatch_json_request(&synth, state);
    }

    let req: IpcRequest = match serde_json::from_str(raw) {
        Ok(r) => r,
        Err(e) => {
            return (
                IpcResponse::err(String::new(), "ERR_BAD_PAYLOAD", format!("Invalid JSON: {}", e))
                    .to_ndjson(),
                false,
            );
        }
    };

    if req.v != PROTOCOL_VERSION {
        return (
            IpcResponse::err(
                req.id,
                "ERR_UNSUPPORTED_VERSION",
                format!(
                    "Unsupported protocol version {} (expected {})",
                    req.v, PROTOCOL_VERSION
                ),
            )
            .to_ndjson(),
            false,
        );
    }

    if state.shutting_down.load(Ordering::SeqCst) && !req.cmd.eq_ignore_ascii_case("SHUTDOWN") {
        return (
            IpcResponse::err(req.id, "ERR_SHUTTING_DOWN", "Worker is shutting down").to_ndjson(),
            false,
        );
    }

    let cmd_upper = req.cmd.trim().to_ascii_uppercase();
    match cmd_upper.as_str() {
        "PING" => {
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "workerVersion": WORKER_VERSION,
                    "protocol": PROTOCOL_VERSION,
                    "pid": std::process::id(),
                    "uptimeSec": state.start_time.elapsed().as_secs(),
                    "ipcOnly": state.ipc_only,
                    "trayActive": state.tray_active.load(Ordering::Relaxed)
                }),
            );
            (resp.to_ndjson(), false)
        }
        "GET_STATUS" => {
            let mons = enumerate_monitors();
            let mon_list: Vec<Value> = mons
                .iter()
                .map(|m| {
                    json!({
                        "deviceName": m.device_name,
                        "left": m.left,
                        "top": m.top,
                        "width": m.width,
                        "height": m.height,
                        "dpi": m.dpi,
                        "isPrimary": m.is_primary
                    })
                })
                .collect();
            let access_code = state.notif_access.load(Ordering::Relaxed);
            let access_str = match access_code {
                1 => "Allowed",
                2 => "Denied",
                _ => "Unknown",
            };
            let notif_mode = if state.ipc_only {
                "DisabledIpcOnly"
            } else if state.notif_event_mode.load(Ordering::Relaxed) {
                "Event+BackupPoll"
            } else {
                "ActivePoll"
            };
            let (rev, client_rev, profiles_cnt, tracked_cnt, use_rust_game, use_rust_glow) = state
                .config
                .read()
                .map(|c| {
                    (
                        c.revision,
                        c.client_revision,
                        c.profiles.len(),
                        c.settings.TrackedGames.len(),
                        c.settings.UseRustGameDetection,
                        c.settings.UseRustGlowEngine,
                    )
                })
                .unwrap_or((0, 0, 0, 0, false, false));

            let pending_master = state.pending_master_enabled.lock().ok().and_then(|g| *g);
            let pending_gaming = state.pending_gaming_mode.lock().ok().and_then(|g| *g);
            let active_game_info = state.game_detector.current_active_game();
            let active_game_name = active_game_info.as_ref().map(|g| g.name.clone());

            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "workerVersion": WORKER_VERSION,
                    "protocol": PROTOCOL_VERSION,
                    "pid": std::process::id(),
                    "uptimeSec": state.start_time.elapsed().as_secs(),
                    "ipcOnly": state.ipc_only,
                    "trayActive": state.tray_active.load(Ordering::Relaxed),
                    "taskbarRestores": state.taskbar_restores.load(Ordering::Relaxed),
                    "notifAccess": access_str,
                    "notifMode": notif_mode,
                    "historyCount": state.notif_history.load(Ordering::Relaxed),
                    "monitors": mon_list,
                    "activeGame": active_game_name,
                    "activeGameInfo": active_game_info,
                    "useRustGameDetection": use_rust_game,
                    "useRustGlowEngine": use_rust_glow,
                    "glowActive": state.glow_engine.is_animating() || state.glow_engine.active_window_count() > 0,
                    "activeOverlayWindows": state.glow_engine.active_window_count(),
                    "masterEnabled": state.effective_master_enabled(),
                    "gamingModeEnabled": state.effective_gaming_mode(),
                    "pendingUiSync": state.pending_ui_sync.load(Ordering::Relaxed),
                    "pendingMasterEnabled": pending_master,
                    "pendingGamingMode": pending_gaming,
                    "settingsRevision": rev,
                    "clientRevision": client_rev,
                    "profilesCount": profiles_cnt,
                    "trackedGamesCount": tracked_cnt,
                    "stats": {
                        "polls": state.notif_polls.load(Ordering::Relaxed),
                        "newNotifs": state.notif_new.load(Ordering::Relaxed),
                        "removedNotifs": state.notif_removed.load(Ordering::Relaxed),
                        "duplicatesSuppressed": state.notif_duplicates_suppressed.load(Ordering::Relaxed),
                        "deniedChecks": state.notif_denied_checks.load(Ordering::Relaxed),
                        "activeListenerCount": state.active_listener_count.load(Ordering::Relaxed),
                        "accessTransitions": state.notif_access_transitions.load(Ordering::Relaxed),
                        "glowsTriggered": state.glow_engine.total_triggers.load(Ordering::Relaxed),
                        "glowsCompleted": state.glow_engine.completed_count.load(Ordering::Relaxed),
                        "glowsDropped": state.glow_engine.dropped_count.load(Ordering::Relaxed),
                        "glowsQueued": state.glow_engine.queued_count.load(Ordering::Relaxed),
                        "glowsExtended": state.glow_engine.extended_count.load(Ordering::Relaxed),
                        "glowsRestarted": state.glow_engine.restarted_count.load(Ordering::Relaxed),
                        "lastStartLatencyUs": state.glow_engine.last_start_latency_us.load(Ordering::Relaxed),
                        "lastFrameCount": state.glow_engine.last_frame_count.load(Ordering::Relaxed),
                        "lastAvgFrameUs": state.glow_engine.last_avg_frame_us.load(Ordering::Relaxed),
                        "windowsCreated": state.glow_engine.windows_created.load(Ordering::Relaxed),
                        "windowsDestroyed": state.glow_engine.windows_destroyed.load(Ordering::Relaxed),
                        "dibsCreated": state.glow_engine.dibs_created.load(Ordering::Relaxed),
                        "dibsDestroyed": state.glow_engine.dibs_destroyed.load(Ordering::Relaxed),
                        "monitorChanges": state.glow_engine.monitor_changes.load(Ordering::Relaxed),
                        "gameScans": state.game_detector.total_scans.load(Ordering::Relaxed),
                        "gameDetections": state.game_detector.total_detections.load(Ordering::Relaxed),
                        "gameCloses": state.game_detector.total_closes.load(Ordering::Relaxed),
                        "lastGameScanLatencyUs": state.game_detector.last_scan_latency_us.load(Ordering::Relaxed),
                        "lastGameDetectLatencyUs": state.game_detector.last_detect_latency_us.load(Ordering::Relaxed)
                    }
                }),
            );
            (resp.to_ndjson(), false)
        }
        "RELOAD_SETTINGS" | "SETTINGS_CHANGED" => {
            let clear_sync = req
                .payload
                .get("clearPendingUiSync")
                .and_then(|v| v.as_bool())
                .unwrap_or(false);
            let client_rev = req
                .payload
                .get("clientRevision")
                .and_then(|v| v.as_u64());
            match state.reload_config_from_disk(client_rev, clear_sync) {
                Ok(out) => {
                    let resp = IpcResponse::ok(
                        req.id,
                        json!({
                            "reloaded": out.reloaded,
                            "staleIgnored": out.stale_ignored,
                            "profilesCount": out.profiles_count,
                            "trackedGamesCount": out.tracked_count,
                            "settingsRevision": out.settings_revision,
                            "clientRevision": out.client_revision,
                            "schemaVersion": out.schema_version,
                            "pendingUiSync": state.pending_ui_sync.load(Ordering::Relaxed)
                        }),
                    );
                    (resp.to_ndjson(), false)
                }
                Err((code, msg)) => (IpcResponse::err(req.id, code, msg).to_ndjson(), false),
            }
        }
        "SET_MASTER_ENABLED" => {
            let Some(enabled) = req.payload.get("enabled").and_then(|v| v.as_bool()) else {
                return (
                    IpcResponse::err(req.id, "ERR_BAD_PAYLOAD", "Missing 'enabled' boolean")
                        .to_ndjson(),
                    false,
                );
            };
            let mark_pending = req
                .payload
                .get("pendingUiSync")
                .and_then(|v| v.as_bool())
                .unwrap_or(true);
            state.set_master_override(enabled, mark_pending);
            let rev = state.config.read().map(|c| c.revision).unwrap_or(0);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "masterEnabled": state.effective_master_enabled(),
                        "pendingUiSync": state.pending_ui_sync.load(Ordering::Relaxed),
                        "settingsRevision": rev
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "SET_GAMING_MODE" => {
            let Some(enabled) = req.payload.get("enabled").and_then(|v| v.as_bool()) else {
                return (
                    IpcResponse::err(req.id, "ERR_BAD_PAYLOAD", "Missing 'enabled' boolean")
                        .to_ndjson(),
                    false,
                );
            };
            let mark_pending = req
                .payload
                .get("pendingUiSync")
                .and_then(|v| v.as_bool())
                .unwrap_or(true);
            state.set_gaming_override(enabled, mark_pending);
            let rev = state.config.read().map(|c| c.revision).unwrap_or(0);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "gamingModeEnabled": state.effective_gaming_mode(),
                        "pendingUiSync": state.pending_ui_sync.load(Ordering::Relaxed),
                        "settingsRevision": rev
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "NOTIFICATION_EVENT" => {
            // Optional synthetic injection or test access override strictly for isolated tests
            if let Some(ov) = req.payload.get("setTestAccess").and_then(|v| v.as_str()) {
                let code = match ov.trim().to_ascii_lowercase().as_str() {
                    "allowed" => 1u64,
                    "denied" => 2u64,
                    "unknown" => 0u64,
                    "clear" | "none" => u64::MAX,
                    _ => {
                        return (
                            IpcResponse::err(req.id, "ERR_BAD_PAYLOAD", "Invalid setTestAccess value")
                                .to_ndjson(),
                            false,
                        );
                    }
                };
                state.test_access_override.store(code, Ordering::SeqCst);
                state.notif_event_signaled.store(true, Ordering::Release);
                return (
                    IpcResponse::ok(req.id, json!({ "testAccessOverride": ov })).to_ndjson(),
                    false,
                );
            }

            if let Some(inj) = req.payload.get("inject") {
                let kind = inj.get("kind").and_then(|v| v.as_str()).unwrap_or("Added");
                let nid = inj.get("notificationId").and_then(|v| v.as_u64()).unwrap_or(1) as u32;
                let aid = inj
                    .get("appId")
                    .and_then(|v| v.as_str())
                    .unwrap_or("TestApp")
                    .to_string();
                let aname = inj
                    .get("appName")
                    .and_then(|v| v.as_str())
                    .unwrap_or(&aid)
                    .to_string();
                let ts = inj
                    .get("timestampUnixMs")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(0);
                let seq = push_notification_event(state, kind, nid, aid, aname, ts);
                return (
                    IpcResponse::ok(req.id, json!({ "injectedSeq": seq })).to_ndjson(),
                    false,
                );
            }

            let ack_seq = req.payload.get("ackSeq").and_then(|v| v.as_u64()).unwrap_or(0);
            let max_count = req
                .payload
                .get("maxCount")
                .and_then(|v| v.as_u64())
                .unwrap_or(32)
                .clamp(1, 128) as usize;

            let mut out_events: Vec<NotificationEventRecord> = Vec::new();
            if let Ok(mut q) = state.notif_events.lock() {
                if ack_seq > 0 {
                    while q.front().map(|e| e.seq <= ack_seq).unwrap_or(false) {
                        q.pop_front();
                    }
                }
                for ev in q.iter().filter(|e| e.seq > ack_seq).take(max_count) {
                    out_events.push(ev.clone());
                }
            }
            let latest_seq = out_events.last().map(|e| e.seq).unwrap_or(ack_seq);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "events": out_events,
                        "latestSeq": latest_seq
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "TRAY_ACTION" => {
            let action = req
                .payload
                .get("action")
                .and_then(|v| v.as_str())
                .unwrap_or("Drain");
            let ack_seq = req.payload.get("ackSeq").and_then(|v| v.as_u64()).unwrap_or(0);

            if action.eq_ignore_ascii_case("Drain") {
                let mut actions: Vec<TrayActionRecord> = Vec::new();
                if let Ok(mut q) = state.tray_actions.lock() {
                    if ack_seq > 0 {
                        while q.front().map(|a| a.seq <= ack_seq).unwrap_or(false) {
                            q.pop_front();
                        }
                    }
                    for a in q.iter().filter(|a| a.seq > ack_seq).take(64) {
                        actions.push(a.clone());
                    }
                }
                let latest = actions.last().map(|a| a.seq).unwrap_or(ack_seq);
                return (
                    IpcResponse::ok(req.id, json!({ "actions": actions, "latestSeq": latest }))
                        .to_ndjson(),
                    false,
                );
            }

            let mut should_shutdown = false;
            let data = match action.to_ascii_lowercase().as_str() {
                "leftclick" | "doubleclick" | "openui" => {
                    let (launched, signaled, pid) = state.open_or_signal_ui(Some("General"), true);
                    json!({ "executed": action, "launched": launched, "signaledExisting": signaled, "uiPid": pid })
                }
                "openupdates" => {
                    let (launched, signaled, pid) = state.open_or_signal_ui(Some("Advanced"), false);
                    json!({ "executed": "OpenUpdates", "launched": launched, "signaledExisting": signaled, "uiPid": pid })
                }
                "togglemaster" => {
                    let next = !state.effective_master_enabled();
                    state.set_master_override(next, true);
                    json!({ "executed": "ToggleMaster", "masterEnabled": next, "pendingUiSync": true })
                }
                "togglegaming" => {
                    let next = !state.effective_gaming_mode();
                    state.set_gaming_override(next, true);
                    json!({ "executed": "ToggleGaming", "gamingModeEnabled": next, "pendingUiSync": true })
                }
                "testanimation" => {
                    let seq = state.push_tray_action("TestAnimation", None);
                    json!({ "executed": "TestAnimation", "actionSeq": seq })
                }
                "simulatetaskbarcreated" => {
                    (state.simulate_taskbar_cb)();
                    json!({
                        "executed": "SimulateTaskbarCreated",
                        "taskbarRestores": state.taskbar_restores.load(Ordering::Relaxed)
                    })
                }
                "exit" => {
                    state.push_tray_action("Exit", None);
                    state.shutting_down.store(true, Ordering::SeqCst);
                    should_shutdown = true;
                    json!({ "executed": "Exit", "bye": true })
                }
                _ => {
                    return (
                        IpcResponse::err(
                            req.id,
                            "ERR_BAD_PAYLOAD",
                            format!("Unknown tray action: {}", action),
                        )
                        .to_ndjson(),
                        false,
                    );
                }
            };
            (IpcResponse::ok(req.id, data).to_ndjson(), should_shutdown)
        }
        "OPEN_UI" => {
            let nav_tag = req
                .payload
                .get("navigateTag")
                .and_then(|v| v.as_str())
                .unwrap_or("General");
            let (launched, signaled, pid) = state.open_or_signal_ui(Some(nav_tag), false);
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "launched": launched,
                    "alreadyRunning": signaled,
                    "uiPid": pid,
                    "navigateTag": nav_tag
                }),
            );
            (resp.to_ndjson(), false)
        }
        "PLAY_GLOW" => {
            let args: PlayGlowArgs = match serde_json::from_value(req.payload) {
                Ok(a) => a,
                Err(e) => {
                    return (
                        IpcResponse::err(
                            req.id,
                            "ERR_BAD_PAYLOAD",
                            format!("Invalid PLAY_GLOW payload: {}", e),
                        )
                        .to_ndjson(),
                        false,
                    );
                }
            };
            let mut settings = args.settings.unwrap_or_else(|| {
                state
                    .config
                    .read()
                    .map(|c| c.settings.clone())
                    .unwrap_or_default()
            });
            settings.MasterEnabled = state.effective_master_enabled();
            settings.GamingModeEnabled = state.effective_gaming_mode();

            let active_from_detector = state.game_detector.current_active_game();
            let eff_is_game_running = args.is_game_running
                || (settings.GamingModeEnabled && active_from_detector.is_some());
            let eff_active_id: Option<String> = args
                .active_game_id
                .or_else(|| active_from_detector.map(|g| g.identifier));

            let outcome = state.glow_engine.trigger(
                &args.profile,
                &settings,
                eff_is_game_running,
                eff_active_id.as_deref(),
                args.is_test,
                None,
            );
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "action": outcome.action,
                    "effectiveStyle": outcome.style,
                    "effectiveDurationMs": outcome.duration_ms,
                    "effectiveIntensity": outcome.intensity,
                    "effectiveCornerRadius": outcome.corner_radius,
                    "monitors": outcome.monitors
                }),
            );
            (resp.to_ndjson(), false)
        }
        "STOP_GLOW" => {
            let before = state.glow_engine.active_window_count();
            state.glow_engine.stop_all();
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "stopped": true,
                    "clearedWindows": before
                }),
            );
            (resp.to_ndjson(), false)
        }
        "SET_GLOW_CONFIG" => {
            // Runtime in-memory update only — worker NEVER writes settings.json to disk.
            if let Ok(mut cfg) = state.config.write() {
                if let Some(v) = req.payload.get("oledMode").and_then(|v| v.as_bool()) {
                    cfg.settings.OledMode = v;
                }
                if let Some(v) = req.payload.get("reduceMotion").and_then(|v| v.as_bool()) {
                    cfg.settings.ReduceMotion = v;
                }
                if let Some(v) = req.payload.get("reduceGlow").and_then(|v| v.as_bool()) {
                    cfg.settings.ReduceGlow = v;
                }
                if let Some(v) = req.payload.get("cornerRadius").and_then(|v| v.as_f64()) {
                    cfg.settings.CornerRadius = v.clamp(0.0, 48.0);
                }
                if let Some(v) = req.payload.get("monitorMode") {
                    if let Ok(mm) = serde_json::from_value::<MonitorMode>(v.clone()) {
                        cfg.settings.MonitorMode = mm;
                    }
                }
                if let Some(v) = req.payload.get("burstMode") {
                    if let Ok(bm) = serde_json::from_value::<BurstMode>(v.clone()) {
                        cfg.settings.BurstMode = bm;
                    }
                }
                if let Some(v) = req.payload.get("useRustGlowEngine").and_then(|v| v.as_bool()) {
                    cfg.settings.UseRustGlowEngine = v;
                }
                if let Some(v) = req.payload.get("useRustGameDetection").and_then(|v| v.as_bool()) {
                    cfg.settings.UseRustGameDetection = v;
                }
                cfg.revision += 1;
            }
            let rev = state.config.read().map(|c| c.revision).unwrap_or(0);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "updated": true,
                        "inMemoryOnly": true,
                        "settingsRevision": rev
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "SET_GAME_GLOW_OVERRIDE" => {
            // Runtime in-memory update only — C# SettingsService remains sole disk writer.
            let Some(game_id) = req
                .payload
                .get("gameIdentifier")
                .and_then(|v| v.as_str())
                .map(AppSettings::normalize_game_identifier)
                .filter(|s| !s.is_empty())
            else {
                return (
                    IpcResponse::err(req.id, "ERR_BAD_PAYLOAD", "Missing 'gameIdentifier'")
                        .to_ndjson(),
                    false,
                );
            };
            let remove = req
                .payload
                .get("remove")
                .and_then(|v| v.as_bool())
                .unwrap_or(false);
            if let Ok(mut cfg) = state.config.write() {
                if remove {
                    cfg.settings
                        .GameOverrides
                        .retain(|k, _| AppSettings::normalize_game_identifier(k) != game_id);
                } else if let Some(ov_val) = req.payload.get("override") {
                    match serde_json::from_value::<GameGlowOverride>(ov_val.clone()) {
                        Ok(mut ov) => {
                            ov.GameIdentifier = game_id.clone();
                            cfg.settings.GameOverrides.insert(game_id.clone(), ov);
                        }
                        Err(e) => {
                            return (
                                IpcResponse::err(
                                    req.id,
                                    "ERR_BAD_PAYLOAD",
                                    format!("Invalid GameGlowOverride: {}", e),
                                )
                                .to_ndjson(),
                                false,
                            );
                        }
                    }
                }
                cfg.revision += 1;
            }
            let rev = state.config.read().map(|c| c.revision).unwrap_or(0);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "updated": true,
                        "gameIdentifier": game_id,
                        "removed": remove,
                        "inMemoryOnly": true,
                        "settingsRevision": rev
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "GLOW_STATUS" | "GLOW_STARTED" | "GLOW_STOPPED" | "MONITOR_CHANGED" => {
            if cmd_upper == "MONITOR_CHANGED"
                && req
                    .payload
                    .get("trigger")
                    .and_then(|v| v.as_bool())
                    .unwrap_or(false)
            {
                let count = state.glow_engine.notify_monitor_changed();
                return (
                    IpcResponse::ok(
                        req.id,
                        json!({
                            "monitorChanged": true,
                            "monitorsDetected": count,
                            "monitorChanges": state.glow_engine.monitor_changes.load(Ordering::Relaxed)
                        }),
                    )
                    .to_ndjson(),
                    false,
                );
            }
            let ack_seq = req.payload.get("ackSeq").and_then(|v| v.as_u64()).unwrap_or(0);
            let max_count = req
                .payload
                .get("maxCount")
                .and_then(|v| v.as_u64())
                .unwrap_or(32)
                .clamp(1, 128) as usize;
            let (events, latest_seq) = state.glow_engine.drain_events(ack_seq, max_count);
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "glowActive": state.glow_engine.is_animating() || state.glow_engine.active_window_count() > 0,
                        "activeOverlayWindows": state.glow_engine.active_window_count(),
                        "totalTriggers": state.glow_engine.total_triggers.load(Ordering::Relaxed),
                        "completedCount": state.glow_engine.completed_count.load(Ordering::Relaxed),
                        "droppedCount": state.glow_engine.dropped_count.load(Ordering::Relaxed),
                        "queuedCount": state.glow_engine.queued_count.load(Ordering::Relaxed),
                        "extendedCount": state.glow_engine.extended_count.load(Ordering::Relaxed),
                        "restartedCount": state.glow_engine.restarted_count.load(Ordering::Relaxed),
                        "lastStartLatencyUs": state.glow_engine.last_start_latency_us.load(Ordering::Relaxed),
                        "lastDurationMs": state.glow_engine.last_duration_ms.load(Ordering::Relaxed),
                        "lastFrameCount": state.glow_engine.last_frame_count.load(Ordering::Relaxed),
                        "lastAvgFrameUs": state.glow_engine.last_avg_frame_us.load(Ordering::Relaxed),
                        "windowsCreated": state.glow_engine.windows_created.load(Ordering::Relaxed),
                        "windowsDestroyed": state.glow_engine.windows_destroyed.load(Ordering::Relaxed),
                        "dibsCreated": state.glow_engine.dibs_created.load(Ordering::Relaxed),
                        "dibsDestroyed": state.glow_engine.dibs_destroyed.load(Ordering::Relaxed),
                        "monitorChanges": state.glow_engine.monitor_changes.load(Ordering::Relaxed),
                        "monitorsDetected": state.glow_engine.monitors_detected.load(Ordering::Relaxed),
                        "events": events,
                        "latestSeq": latest_seq
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "SCAN_GAMES" => {
            let (games, elapsed_ms) = state.game_detector.scan_installed_games();
            let count = games.len();
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "games": games,
                    "count": count,
                    "elapsedMs": elapsed_ms,
                    "phase3aStub": false
                }),
            );
            (resp.to_ndjson(), false)
        }
        "GET_GAMES" => {
            let tracked = state
                .config
                .read()
                .map(|c| c.settings.TrackedGames.clone())
                .unwrap_or_default();
            let cached = state
                .game_detector
                .cached_games
                .read()
                .map(|c| c.clone())
                .unwrap_or_default();
            let active_info = state.game_detector.current_active_game();
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "activeGame": active_info.as_ref().map(|g| g.name.clone()),
                    "activeGameInfo": active_info,
                    "cachedGames": cached,
                    "trackedGames": tracked
                }),
            );
            (resp.to_ndjson(), false)
        }
        "GAME_STATUS" | "GAME_DETECTED" | "GAME_CLOSED" => {
            if let Some(sim_val) = req.payload.get("setSimulatedGame") {
                if sim_val.is_null() {
                    state.game_detector.set_simulated_game(None);
                } else if let Some(name) = sim_val.as_str() {
                    let trimmed = name.trim();
                    if trimmed.is_empty() {
                        state.game_detector.set_simulated_game(None);
                    } else {
                        let exe = req
                            .payload
                            .get("executablePath")
                            .and_then(|v| v.as_str())
                            .unwrap_or(trimmed)
                            .to_string();
                        let launcher = req
                            .payload
                            .get("launcher")
                            .and_then(|v| v.as_str())
                            .unwrap_or("Manual")
                            .to_string();
                        state.game_detector.set_simulated_game(Some(ActiveGameInfo {
                            name: trimmed.to_string(),
                            identifier: AppSettings::normalize_game_identifier(&exe),
                            executable_path: exe,
                            launcher,
                            pid: 0,
                        }));
                    }
                }
            }
            let ack_seq = req.payload.get("ackSeq").and_then(|v| v.as_u64()).unwrap_or(0);
            let max_count = req
                .payload
                .get("maxCount")
                .and_then(|v| v.as_u64())
                .unwrap_or(32)
                .clamp(1, 128) as usize;
            let (events, latest_seq) = state.game_detector.drain_events(ack_seq, max_count);
            let gaming_enabled = state.effective_gaming_mode();
            let active_info = state.game_detector.current_active_game();
            (
                IpcResponse::ok(
                    req.id,
                    json!({
                        "gamingModeEnabled": gaming_enabled,
                        "isGameRunning": state.game_detector.is_game_running(gaming_enabled),
                        "activeGame": active_info,
                        "totalScans": state.game_detector.total_scans.load(Ordering::Relaxed),
                        "totalDetections": state.game_detector.total_detections.load(Ordering::Relaxed),
                        "totalCloses": state.game_detector.total_closes.load(Ordering::Relaxed),
                        "lastScanLatencyUs": state.game_detector.last_scan_latency_us.load(Ordering::Relaxed),
                        "lastDetectLatencyUs": state.game_detector.last_detect_latency_us.load(Ordering::Relaxed),
                        "events": events,
                        "latestSeq": latest_seq
                    }),
                )
                .to_ndjson(),
                false,
            )
        }
        "SHUTDOWN" => {
            let already = state.shutting_down.swap(true, Ordering::SeqCst);
            let resp = IpcResponse::ok(
                req.id,
                json!({
                    "bye": true,
                    "alreadyShuttingDown": already
                }),
            );
            (resp.to_ndjson(), true)
        }
        _ => (
            IpcResponse::err(
                req.id,
                "ERR_UNKNOWN_CMD",
                format!("Unknown command: {}", req.cmd),
            )
            .to_ndjson(),
            false,
        ),
    }
}

fn handle_client(pipe_raw: isize, state: Arc<WorkerState>) {
    unsafe {
        let h_pipe = HANDLE(pipe_raw as *mut std::ffi::c_void);

        if !verify_client_same_session(h_pipe) {
            let err_resp = IpcResponse::err(
                String::new(),
                "ERR_UNAUTHORIZED",
                "Cross-session IPC access denied",
            )
            .to_ndjson();
            let mut written = 0u32;
            let _ = WriteFile(h_pipe, Some(err_resp.as_bytes()), Some(&mut written), None);
            let _ = FlushFileBuffers(h_pipe);
            let _ = DisconnectNamedPipe(h_pipe);
            let _ = CloseHandle(h_pipe);
            return;
        }

        let (resp_str, should_shutdown) = match read_message_bounded(h_pipe) {
            Ok(raw) => dispatch_json_request(&raw, &state),
            Err("ERR_EOF") => {
                let _ = DisconnectNamedPipe(h_pipe);
                let _ = CloseHandle(h_pipe);
                return;
            }
            Err(code) => (
                IpcResponse::err(String::new(), code, "Invalid or oversized IPC message")
                    .to_ndjson(),
                false,
            ),
        };

        let mut written = 0u32;
        let _ = WriteFile(h_pipe, Some(resp_str.as_bytes()), Some(&mut written), None);
        let _ = FlushFileBuffers(h_pipe);
        let _ = DisconnectNamedPipe(h_pipe);
        let _ = CloseHandle(h_pipe);

        if should_shutdown {
            (state.request_shutdown_cb)("IPC_SHUTDOWN");
        }
    }
}

pub fn run_ipc_server(pipe_name: String, state: Arc<WorkerState>) {
    let pipe_wide = to_wide_null(&pipe_name);
    (state.log_cb)(&format!("IPC server listening on {}", pipe_name));

    while state.running.load(Ordering::Relaxed) {
        unsafe {
            let sd_opt = build_current_user_sd();
            let mut sa = SECURITY_ATTRIBUTES {
                nLength: std::mem::size_of::<SECURITY_ATTRIBUTES>() as u32,
                lpSecurityDescriptor: sd_opt.map(|s| s.0).unwrap_or(std::ptr::null_mut()),
                bInheritHandle: false.into(),
            };
            let sa_ptr = if sd_opt.is_some() {
                Some(&sa as *const SECURITY_ATTRIBUTES)
            } else {
                None
            };

            let h_pipe = CreateNamedPipeW(
                PCWSTR(pipe_wide.as_ptr()),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                8,
                MAX_MESSAGE_BYTES as u32,
                MAX_MESSAGE_BYTES as u32,
                2000,
                sa_ptr,
            );

            if let Some(sd) = sd_opt {
                let _ = LocalFree(HLOCAL(sd.0));
            }
            let _ = &mut sa;

            if h_pipe == INVALID_HANDLE_VALUE {
                thread::sleep(Duration::from_millis(100));
                continue;
            }

            let connected = ConnectNamedPipe(h_pipe, None).is_ok() || GetLastError().0 == 535;
            if connected {
                let pipe_raw = h_pipe.0 as isize;
                let st = Arc::clone(&state);
                thread::spawn(move || handle_client(pipe_raw, st));
            } else {
                let _ = CloseHandle(h_pipe);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_test_state() -> Arc<WorkerState> {
        static TEST_RUN: AtomicBool = AtomicBool::new(true);
        static TEST_TRAY: AtomicBool = AtomicBool::new(true);
        static TEST_REST: AtomicU64 = AtomicU64::new(0);
        static TEST_ACC: AtomicU64 = AtomicU64::new(1);
        static TEST_POL: AtomicU64 = AtomicU64::new(0);
        static TEST_NEW: AtomicU64 = AtomicU64::new(0);
        static TEST_DEN: AtomicU64 = AtomicU64::new(0);
        Arc::new(WorkerState {
            start_time: Instant::now(),
            running: &TEST_RUN,
            shutting_down: AtomicBool::new(false),
            ipc_only: true,
            config_dir: None,
            ui_exe_path: None,
            reload_lock: Mutex::new(()),
            config: RwLock::new(ConfigSnapshot::default()),
            glow_engine: GlowEngine::new(&TEST_RUN),
            game_detector: GameDetectorState::new(),
            tray_active: &TEST_TRAY,
            taskbar_restores: &TEST_REST,
            notif_access: &TEST_ACC,
            notif_polls: &TEST_POL,
            notif_new: &TEST_NEW,
            notif_denied_checks: &TEST_DEN,
            notif_history: AtomicU64::new(0),
            notif_removed: AtomicU64::new(0),
            notif_duplicates_suppressed: AtomicU64::new(0),
            notif_event_mode: AtomicBool::new(false),
            notif_event_signaled: AtomicBool::new(false),
            active_listener_count: AtomicU64::new(0),
            notif_access_transitions: AtomicU64::new(0),
            test_access_override: AtomicU64::new(u64::MAX),
            next_notif_seq: AtomicU64::new(0),
            notif_events: Mutex::new(VecDeque::new()),
            next_tray_seq: AtomicU64::new(0),
            tray_actions: Mutex::new(VecDeque::new()),
            pending_master_enabled: Mutex::new(None),
            pending_gaming_mode: Mutex::new(None),
            pending_ui_sync: AtomicBool::new(false),
            last_ui_open_ms: AtomicU64::new(0),
            simulate_taskbar_cb: || {},
            request_shutdown_cb: |_| {},
            log_cb: |_| {},
        })
    }

    #[test]
    fn test_ping_and_version_validation() {
        let st = make_test_state();
        let (resp, shut) =
            dispatch_json_request(r#"{"v":1,"id":"1","cmd":"PING","payload":{}}"#, &st);
        assert!(!shut);
        let v: Value = serde_json::from_str(&resp).unwrap();
        assert_eq!(v["ok"], true);
        assert_eq!(v["data"]["protocol"], 1);

        let (bad_v, _) =
            dispatch_json_request(r#"{"v":99,"id":"2","cmd":"PING","payload":{}}"#, &st);
        let bv: Value = serde_json::from_str(&bad_v).unwrap();
        assert_eq!(bv["ok"], false);
        assert_eq!(bv["error"]["code"], "ERR_UNSUPPORTED_VERSION");
    }

    #[test]
    fn test_malformed_json_and_unknown_cmd() {
        let st = make_test_state();
        let (bad, _) = dispatch_json_request("{not-valid-json", &st);
        let v: Value = serde_json::from_str(&bad).unwrap();
        assert_eq!(v["ok"], false);
        assert_eq!(v["error"]["code"], "ERR_BAD_PAYLOAD");

        let (unk, _) =
            dispatch_json_request(r#"{"v":1,"id":"3","cmd":"NO_SUCH_CMD","payload":{}}"#, &st);
        let u: Value = serde_json::from_str(&unk).unwrap();
        assert_eq!(u["ok"], false);
        assert_eq!(u["error"]["code"], "ERR_UNKNOWN_CMD");
    }

    #[test]
    fn test_pending_tray_toggles_do_not_touch_disk_and_ack_events() {
        let st = make_test_state();
        let (r1, _) = dispatch_json_request(
            r#"{"v":1,"id":"t1","cmd":"SET_MASTER_ENABLED","payload":{"enabled":false,"pendingUiSync":true}}"#,
            &st,
        );
        let v1: Value = serde_json::from_str(&r1).unwrap();
        assert_eq!(v1["ok"], true);
        assert_eq!(v1["data"]["masterEnabled"], false);
        assert_eq!(v1["data"]["pendingUiSync"], true);

        // Inject and drain notification event with ackSeq
        let _ = dispatch_json_request(
            r#"{"v":1,"id":"n1","cmd":"NOTIFICATION_EVENT","payload":{"inject":{"notificationId":42,"appId":"Discord","appName":"Discord"}}}"#,
            &st,
        );
        let (ev_resp, _) = dispatch_json_request(
            r#"{"v":1,"id":"n2","cmd":"NOTIFICATION_EVENT","payload":{"ackSeq":0}}"#,
            &st,
        );
        let ev_v: Value = serde_json::from_str(&ev_resp).unwrap();
        assert_eq!(ev_v["data"]["events"].as_array().unwrap().len(), 1);
        let seq = ev_v["data"]["latestSeq"].as_u64().unwrap();

        // Acknowledge seq -> subsequent drain returns 0 (no duplicate!)
        let (ev_resp2, _) = dispatch_json_request(
            &format!(r#"{{"v":1,"id":"n3","cmd":"NOTIFICATION_EVENT","payload":{{"ackSeq":{}}}}}"#, seq),
            &st,
        );
        let ev_v2: Value = serde_json::from_str(&ev_resp2).unwrap();
        assert_eq!(ev_v2["data"]["events"].as_array().unwrap().len(), 0);
    }

    #[test]
    fn test_phase3d_ipc_game_and_glow_commands() {
        let st = make_test_state();

        // 1. SET_GLOW_CONFIG (in-memory only)
        let (r_cfg, _) = dispatch_json_request(
            r#"{"v":1,"id":"g1","cmd":"SET_GLOW_CONFIG","payload":{"oledMode":true,"reduceMotion":true,"cornerRadius":20,"monitorMode":"AllMonitors","burstMode":"Queue"}}"#,
            &st,
        );
        let v_cfg: Value = serde_json::from_str(&r_cfg).unwrap();
        assert_eq!(v_cfg["ok"], true);
        assert_eq!(v_cfg["data"]["inMemoryOnly"], true);

        // 2. SET_GAME_GLOW_OVERRIDE (in-memory only)
        let (r_ov, _) = dispatch_json_request(
            r##"{"v":1,"id":"g2","cmd":"SET_GAME_GLOW_OVERRIDE","payload":{"gameIdentifier":"cs2.exe","override":{"CustomGlowEnabled":true,"DurationMs":1500,"Intensity":0.7,"ColorHex":"#FF3366","Style":"Comet"}}}"##,
            &st,
        );
        let v_ov: Value = serde_json::from_str(&r_ov).unwrap();
        assert_eq!(v_ov["ok"], true);
        assert_eq!(v_ov["data"]["gameIdentifier"], "cs2.exe");

        // 3. GAME_STATUS with simulated game -> emits GAME_DETECTED then GAME_CLOSED
        let (r_sim1, _) = dispatch_json_request(
            r#"{"v":1,"id":"g3","cmd":"GAME_STATUS","payload":{"setSimulatedGame":"Counter-Strike 2","executablePath":"C:\\Games\\cs2.exe","launcher":"Steam"}}"#,
            &st,
        );
        let v_sim1: Value = serde_json::from_str(&r_sim1).unwrap();
        assert_eq!(v_sim1["ok"], true);
        assert_eq!(v_sim1["data"]["isGameRunning"], true);
        assert_eq!(v_sim1["data"]["events"].as_array().unwrap().len(), 1);
        assert_eq!(v_sim1["data"]["events"][0]["kind"], "GAME_DETECTED");
        let ack1 = v_sim1["data"]["latestSeq"].as_u64().unwrap();

        let (r_sim2, _) = dispatch_json_request(
            &format!(r#"{{"v":1,"id":"g4","cmd":"GAME_STATUS","payload":{{"setSimulatedGame":null,"ackSeq":{}}}}}"#, ack1),
            &st,
        );
        let v_sim2: Value = serde_json::from_str(&r_sim2).unwrap();
        assert_eq!(v_sim2["data"]["isGameRunning"], false);
        assert_eq!(v_sim2["data"]["events"].as_array().unwrap().len(), 1);
        assert_eq!(v_sim2["data"]["events"][0]["kind"], "GAME_CLOSED");

        // 4. MONITOR_CHANGED trigger + GLOW_STATUS drain
        let (r_mon, _) = dispatch_json_request(
            r#"{"v":1,"id":"g5","cmd":"MONITOR_CHANGED","payload":{"trigger":true}}"#,
            &st,
        );
        let v_mon: Value = serde_json::from_str(&r_mon).unwrap();
        assert_eq!(v_mon["ok"], true);
        assert_eq!(v_mon["data"]["monitorChanged"], true);

        let (r_gs, _) = dispatch_json_request(
            r#"{"v":1,"id":"g6","cmd":"GLOW_STATUS","payload":{"ackSeq":0}}"#,
            &st,
        );
        let v_gs: Value = serde_json::from_str(&r_gs).unwrap();
        assert_eq!(v_gs["ok"], true);
        assert!(v_gs["data"]["monitorChanges"].as_u64().unwrap() >= 1);
    }
}
