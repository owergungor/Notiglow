use std::collections::{HashSet, VecDeque};
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};
use serde_json::Value;
use windows::core::{PCWSTR, PWSTR};
use windows::Win32::Foundation::{CloseHandle, ERROR_SUCCESS};
use windows::Win32::Storage::FileSystem::GetLogicalDrives;
use windows::Win32::System::Registry::{
    RegCloseKey, RegEnumKeyExW, RegOpenKeyExW, RegQueryValueExW, HKEY, HKEY_CURRENT_USER,
    HKEY_LOCAL_MACHINE, KEY_READ, REG_SZ, REG_VALUE_TYPE,
};
use windows::Win32::System::Threading::{
    OpenProcess, QueryFullProcessImageNameW, PROCESS_NAME_FORMAT,
    PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows::Win32::UI::WindowsAndMessaging::{
    GetForegroundWindow, GetWindowTextW, GetWindowThreadProcessId,
};

use crate::ipc::WorkerState;
use crate::models::AppSettings;

pub const GAME_POLL_INTERVAL_MS: u64 = 1500;
const MAX_GAME_EVENTS: usize = 128;

const STEAM_IGNORED_APP_IDS: &[&str] = &[
    "228980",  // Steamworks Common Redistributables
    "250820",  // SteamVR
    "1070560", // Steam Linux Runtime
    "1391110", // Steam Linux Runtime - Soldier
    "1628350", // Steam Linux Runtime - Sniper
];

const IGNORED_EXE_PREFIXES: &[&str] = &[
    "unins",
    "setup",
    "install",
    "crashpad",
    "crashreport",
    "crashhandler",
    "unitycrashhandler",
    "dxsetup",
    "vcredist",
    "easyanticheat",
    "eaanticheat",
    "epicgameslauncher",
    "battleye",
    "bugreport",
    "steamerrorreporter",
    "bspzip",
    "vbsp",
    "vrad",
    "vvis",
    "vpk",
    "vtex",
    "studiomdl",
    "shadercompile",
    "hlmv",
    "hammer",
];

const XBOX_IGNORED_EXE_NAMES: &[&str] = &[
    "unins000.exe",
    "uninstall.exe",
    "setup.exe",
    "installer.exe",
    "crashpad_handler.exe",
    "crashreport.exe",
    "crashhandler.exe",
    "unitycrashhandler.exe",
    "unitycrashhandler64.exe",
    "dxsetup.exe",
    "vcredist_x64.exe",
    "vcredist_x86.exe",
    "easyanticheat.exe",
    "battleye.exe",
    "gamelaunchhelper.exe",
    "gamelauncher.exe",
    "appactions.exe",
];

const EXCLUDED_PACKAGE_PREFIXES: &[&str] = &[
    "Microsoft.WindowsStore",
    "Microsoft.GamingApp",
    "Microsoft.GamingServices",
    "Microsoft.XboxGamingOverlay",
    "Microsoft.XboxIdentityProvider",
    "Microsoft.XboxSpeechToTextOverlay",
    "Microsoft.XboxTCUI",
    "Microsoft.XboxGameOverlay",
    "Microsoft.XboxGameCallableUI",
    "Microsoft.WindowsTerminal",
    "Microsoft.GetHelp",
    "Microsoft.Getstarted",
    "Microsoft.ScreenSketch",
    "Microsoft.Paint",
    "Microsoft.WindowsCalculator",
    "Microsoft.WindowsCamera",
    "Microsoft.WindowsAlarms",
    "Microsoft.YourPhone",
    "Microsoft.ZuneVideo",
    "Microsoft.ZuneMusic",
    "Microsoft.People",
    "Microsoft.MicrosoftOfficeHub",
    "Microsoft.MicrosoftSolitaireCollection",
    "MicrosoftWindows.",
    "Microsoft.Windows.",
    "Windows.",
    "crius.Notiglow",
];

const SHARED_ENGINE_EXES: &[&str] = &[
    "hl2.exe",
    "gdklauncher.exe",
    "gamelaunchhelper.exe",
    "shipping.exe",
    "unrealgame.exe",
];

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DetectedGameInfo {
    pub name: String,
    pub executable_path: String,
    pub launcher: String,
    pub app_id: Option<String>,
    pub icon_path: Option<String>,
    pub aum_id: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActiveGameInfo {
    pub name: String,
    pub executable_path: String,
    pub identifier: String,
    pub launcher: String,
    pub pid: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GameEventRecord {
    pub seq: u64,
    pub kind: String, // "GAME_DETECTED" | "GAME_CLOSED"
    pub game_name: String,
    pub executable_path: String,
    pub identifier: String,
    pub launcher: String,
    pub pid: u32,
    pub timestamp_unix_ms: i64,
}

#[derive(Debug, Clone)]
pub struct ForegroundProcessInfo {
    pub pid: u32,
    pub process_name: String, // without .exe
    pub full_path: Option<String>,
    pub window_title: String,
}

pub trait ForegroundProcessProvider {
    fn get_foreground_process(&self) -> Option<ForegroundProcessInfo>;
}

pub struct Win32ForegroundProvider;

impl ForegroundProcessProvider for Win32ForegroundProvider {
    fn get_foreground_process(&self) -> Option<ForegroundProcessInfo> {
        unsafe {
            let hwnd = GetForegroundWindow();
            if hwnd.0.is_null() {
                return None;
            }
            let mut pid = 0u32;
            let _ = GetWindowThreadProcessId(hwnd, Some(&mut pid));
            if pid == 0 {
                return None;
            }

            let mut title_buf = [0u16; 256];
            let tlen = GetWindowTextW(hwnd, &mut title_buf);
            let window_title = if tlen > 0 {
                String::from_utf16_lossy(&title_buf[..tlen as usize])
                    .trim()
                    .to_string()
            } else {
                String::new()
            };

            let mut full_path: Option<String> = None;
            if let Ok(hproc) = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) {
                let mut path_buf = [0u16; 1024];
                let mut size = path_buf.len() as u32;
                if QueryFullProcessImageNameW(
                    hproc,
                    PROCESS_NAME_FORMAT(0),
                    PWSTR(path_buf.as_mut_ptr()),
                    &mut size,
                )
                .is_ok()
                    && size > 0
                {
                    full_path = Some(String::from_utf16_lossy(&path_buf[..size as usize]));
                }
                let _ = CloseHandle(hproc);
            }

            let process_name = full_path
                .as_deref()
                .and_then(|p| Path::new(p).file_stem().and_then(|s| s.to_str()))
                .unwrap_or("")
                .to_string();

            if process_name.is_empty() {
                return None;
            }

            Some(ForegroundProcessInfo {
                pid,
                process_name,
                full_path,
                window_title,
            })
        }
    }
}

pub struct GameDetectorState {
    pub active_game: RwLock<Option<ActiveGameInfo>>,
    pub simulated_game: RwLock<Option<ActiveGameInfo>>,
    pub cached_games: RwLock<Vec<DetectedGameInfo>>,
    pub next_event_seq: AtomicU64,
    pub events: Mutex<VecDeque<GameEventRecord>>,
    pub total_scans: AtomicU64,
    pub total_detections: AtomicU64,
    pub total_closes: AtomicU64,
    pub last_scan_latency_us: AtomicU64,
    pub last_detect_latency_us: AtomicU64,
}

impl GameDetectorState {
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            active_game: RwLock::new(None),
            simulated_game: RwLock::new(None),
            cached_games: RwLock::new(Vec::new()),
            next_event_seq: AtomicU64::new(0),
            events: Mutex::new(VecDeque::with_capacity(64)),
            total_scans: AtomicU64::new(0),
            total_detections: AtomicU64::new(0),
            total_closes: AtomicU64::new(0),
            last_scan_latency_us: AtomicU64::new(0),
            last_detect_latency_us: AtomicU64::new(0),
        })
    }

    pub fn current_active_game(&self) -> Option<ActiveGameInfo> {
        if let Ok(sim) = self.simulated_game.read() {
            if sim.is_some() {
                return sim.clone();
            }
        }
        self.active_game.read().ok().and_then(|g| g.clone())
    }

    pub fn is_game_running(&self, gaming_mode_enabled: bool) -> bool {
        if let Ok(sim) = self.simulated_game.read() {
            if sim.is_some() {
                return true;
            }
        }
        if !gaming_mode_enabled {
            return false;
        }
        self.active_game
            .read()
            .map(|g| g.is_some())
            .unwrap_or(false)
    }

    pub fn set_simulated_game(&self, game: Option<ActiveGameInfo>) {
        let prev = self.current_active_game();
        if let Ok(mut sim) = self.simulated_game.write() {
            *sim = game.clone();
        }
        let next = self.current_active_game();
        self.emit_transition(&prev, &next);
    }

    pub fn push_event(&self, kind: &str, game: &ActiveGameInfo) -> u64 {
        let seq = self.next_event_seq.fetch_add(1, Ordering::SeqCst) + 1;
        let ts = now_unix_ms();
        let rec = GameEventRecord {
            seq,
            kind: kind.to_string(),
            game_name: game.name.clone(),
            executable_path: game.executable_path.clone(),
            identifier: game.identifier.clone(),
            launcher: game.launcher.clone(),
            pid: game.pid,
            timestamp_unix_ms: ts,
        };
        if let Ok(mut q) = self.events.lock() {
            if q.len() >= MAX_GAME_EVENTS {
                q.pop_front();
            }
            q.push_back(rec);
        }
        seq
    }

    pub fn drain_events(&self, ack_seq: u64, max_count: usize) -> (Vec<GameEventRecord>, u64) {
        let mut out = Vec::new();
        if let Ok(mut q) = self.events.lock() {
            if ack_seq > 0 {
                while q.front().map(|e| e.seq <= ack_seq).unwrap_or(false) {
                    q.pop_front();
                }
            }
            for ev in q.iter().filter(|e| e.seq > ack_seq).take(max_count) {
                out.push(ev.clone());
            }
        }
        let latest = out.last().map(|e| e.seq).unwrap_or(ack_seq);
        (out, latest)
    }

    pub fn emit_transition(&self, prev: &Option<ActiveGameInfo>, next: &Option<ActiveGameInfo>) {
        match (prev, next) {
            (None, Some(n)) => {
                self.total_detections.fetch_add(1, Ordering::Relaxed);
                self.push_event("GAME_DETECTED", n);
            }
            (Some(p), None) => {
                self.total_closes.fetch_add(1, Ordering::Relaxed);
                self.push_event("GAME_CLOSED", p);
            }
            (Some(p), Some(n)) if !p.identifier.eq_ignore_ascii_case(&n.identifier) => {
                self.total_closes.fetch_add(1, Ordering::Relaxed);
                self.push_event("GAME_CLOSED", p);
                self.total_detections.fetch_add(1, Ordering::Relaxed);
                self.push_event("GAME_DETECTED", n);
            }
            _ => {}
        }
    }

    /// Evaluates one foreground process check against settings.TrackedGames and updates active_game
    /// with deduplicated GAME_DETECTED / GAME_CLOSED events.
    pub fn poll_foreground_with<P: ForegroundProcessProvider>(
        &self,
        provider: &P,
        settings: &AppSettings,
    ) -> Option<ActiveGameInfo> {
        let t0 = Instant::now();
        if let Ok(sim) = self.simulated_game.read() {
            if sim.is_some() {
                return sim.clone();
            }
        }

        let detected = if !settings.GamingModeEnabled || settings.TrackedGames.is_empty() {
            None
        } else if let Some(fg) = provider.get_foreground_process() {
            let cached = self
                .cached_games
                .read()
                .map(|c| c.clone())
                .unwrap_or_default();
            match_foreground_against_tracked(&fg, &settings.TrackedGames, &cached)
        } else {
            None
        };

        let elapsed_us = t0.elapsed().as_micros() as u64;
        self.last_detect_latency_us
            .store(elapsed_us, Ordering::Relaxed);

        let prev = self.active_game.read().ok().and_then(|g| g.clone());
        if let Ok(mut guard) = self.active_game.write() {
            *guard = detected.clone();
        }
        self.emit_transition(&prev, &detected);
        detected
    }

    /// Runs a full installed-game scan (Steam + Epic + Xbox/Store) and updates cached_games.
    pub fn scan_installed_games(&self) -> (Vec<DetectedGameInfo>, u64) {
        let t0 = Instant::now();
        let games = detect_all_installed_games();
        let elapsed_us = t0.elapsed().as_micros() as u64;
        self.total_scans.fetch_add(1, Ordering::Relaxed);
        self.last_scan_latency_us
            .store(elapsed_us, Ordering::Relaxed);
        if let Ok(mut guard) = self.cached_games.write() {
            *guard = games.clone();
        }
        (games, elapsed_us / 1000)
    }
}

pub fn match_foreground_against_tracked(
    fg: &ForegroundProcessInfo,
    tracked_games: &[String],
    cached_installed: &[DetectedGameInfo],
) -> Option<ActiveGameInfo> {
    let proc_lower = fg.process_name.to_ascii_lowercase();
    let exe_lower = format!("{}.exe", proc_lower);

    for raw in tracked_games {
        let trimmed = raw.trim();
        if trimmed.is_empty() {
            continue;
        }
        let path = Path::new(trimmed);
        let file_name = path
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or(trimmed);
        let file_stem = path
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or(file_name);

        let name_matches = proc_lower.eq_ignore_ascii_case(file_stem)
            || exe_lower.eq_ignore_ascii_case(file_name);
        if !name_matches {
            continue;
        }

        let identifier = AppSettings::normalize_game_identifier(trimmed);
        let launcher = cached_installed
            .iter()
            .find(|g| {
                g.executable_path.eq_ignore_ascii_case(trimmed)
                    || AppSettings::normalize_game_identifier(&g.executable_path) == identifier
            })
            .map(|g| g.launcher.clone())
            .unwrap_or_else(|| "Manual".to_string());

        let display_name = if !fg.window_title.trim().is_empty() {
            fg.window_title.clone()
        } else {
            fg.process_name.clone()
        };

        // If tracked entry has a full path, prefer matching full_path if accessible (matches C# behavior)
        if trimmed.contains('\\') || trimmed.contains('/') {
            if let Some(ref full_proc_path) = fg.full_path {
                if full_proc_path.eq_ignore_ascii_case(trimmed) {
                    return Some(ActiveGameInfo {
                        name: display_name,
                        executable_path: full_proc_path.clone(),
                        identifier,
                        launcher,
                        pid: fg.pid,
                    });
                }
            }
        }

        return Some(ActiveGameInfo {
            name: display_name,
            executable_path: fg
                .full_path
                .clone()
                .unwrap_or_else(|| trimmed.to_string()),
            identifier,
            launcher,
            pid: fg.pid,
        });
    }

    None
}

pub fn run_game_monitor_service(state: Arc<WorkerState>) {
    (state.log_cb)("Game monitor service started (1500ms cadence)");
    let provider = Win32ForegroundProvider;

    // Initial lightweight scan of installed games so launcher metadata is warm
    let _ = state.game_detector.scan_installed_games();

    while state.running.load(Ordering::Relaxed) && !state.shutting_down.load(Ordering::Relaxed) {
        let gaming_enabled = state.effective_gaming_mode();
        if !gaming_enabled {
            if state.game_detector.current_active_game().is_some() {
                let disabled_settings = AppSettings {
                    GamingModeEnabled: false,
                    ..AppSettings::default()
                };
                let _ = state
                    .game_detector
                    .poll_foreground_with(&provider, &disabled_settings);
            }
        } else if let Ok(cfg) = state.config.read() {
            if !cfg.settings.TrackedGames.is_empty()
                || state.game_detector.current_active_game().is_some()
            {
                if cfg.settings.GamingModeEnabled {
                    let _ = state
                        .game_detector
                        .poll_foreground_with(&provider, &cfg.settings);
                } else {
                    let mut override_settings = cfg.settings.clone();
                    override_settings.GamingModeEnabled = true;
                    let _ = state
                        .game_detector
                        .poll_foreground_with(&provider, &override_settings);
                }
            }
        }

        let mut slept = 0u64;
        while slept < GAME_POLL_INTERVAL_MS
            && state.running.load(Ordering::Relaxed)
            && !state.shutting_down.load(Ordering::Relaxed)
        {
            let step = (GAME_POLL_INTERVAL_MS - slept).min(200);
            thread::sleep(Duration::from_millis(step));
            slept += step;
        }
    }
    (state.log_cb)("Game monitor service stopped");
}

// ---------------------------------------------------------------------------
// Installed Game Discovery (Steam, Epic, Xbox / Microsoft Store)
// ---------------------------------------------------------------------------

pub fn is_shared_engine_exe(path_or_exe: &str) -> bool {
    let fname = Path::new(path_or_exe.trim())
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or(path_or_exe.trim())
        .to_ascii_lowercase();
    SHARED_ENGINE_EXES.iter().any(|&s| s == fname)
}

fn known_display_name(key: &str) -> Option<&'static str> {
    match key.trim().to_ascii_lowercase().as_str() {
        "haloinfinite" | "haloinfinite.exe" | "microsoft.254428597cfe2" => Some("Halo Infinite"),
        "portal" | "portal.exe" => Some("Portal"),
        "portal2" | "portal2.exe" => Some("Portal 2"),
        "f1_24" | "f1_24.exe" => Some("F1 24"),
        "forzahorizon4" => Some("Forza Horizon 4"),
        "forzahorizon5" => Some("Forza Horizon 5"),
        "forzahorizon6" => Some("Forza Horizon 6"),
        "assettocorsa" => Some("Assetto Corsa"),
        "stickfight" | "stickfightthegame" => Some("Stick Fight: The Game"),
        _ => None,
    }
}

pub fn deduplicate_detected_games(combined: Vec<DetectedGameInfo>) -> Vec<DetectedGameInfo> {
    let mut unique = Vec::new();
    let mut seen_exes: HashSet<String> = HashSet::new();
    let mut seen_paths: HashSet<String> = HashSet::new();
    let mut seen_names: HashSet<String> = HashSet::new();

    for mut game in combined {
        if game.executable_path.trim().is_empty() {
            continue;
        }
        let exe_lower = Path::new(&game.executable_path)
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();
        let path_lower = game.executable_path.trim().to_ascii_lowercase();
        if exe_lower == "appactions.exe" || path_lower.contains(r"\windows\systemapps\") {
            continue;
        }

        if game.name.trim().is_empty() {
            let stem = Path::new(&game.executable_path)
                .file_stem()
                .and_then(|s| s.to_str())
                .unwrap_or("Unknown Game");
            game.name = known_display_name(stem)
                .map(|s| s.to_string())
                .unwrap_or_else(|| stem.to_string());
        } else if let Some(known) = known_display_name(&game.name) {
            game.name = known.to_string();
        }

        let name_key = game.name.trim().to_ascii_lowercase();
        let shared = is_shared_engine_exe(&exe_lower);

        let exe_dup = !shared && seen_exes.contains(&exe_lower);
        let path_dup = seen_paths.contains(&path_lower);
        let name_dup = !name_key.is_empty() && seen_names.contains(&name_key);

        if !exe_dup && !path_dup && !name_dup {
            if !shared {
                seen_exes.insert(exe_lower);
            }
            seen_paths.insert(path_lower);
            if !name_key.is_empty() {
                seen_names.insert(name_key);
            }
            unique.push(game);
        }
    }

    unique
}

pub fn detect_all_installed_games() -> Vec<DetectedGameInfo> {
    let mut combined = Vec::new();
    combined.extend(detect_steam_games());
    combined.extend(detect_epic_games(None));
    combined.extend(detect_xbox_and_store_games());
    deduplicate_detected_games(combined)
}

// --- Steam ---

fn normalize_vdf_path(raw: &str) -> String {
    raw.replace(r"\\", r"\").replace('/', r"\").trim().to_string()
}

pub fn extract_vdf_value(content: &str, key: &str) -> Option<String> {
    let key_lower = key.to_ascii_lowercase();
    for line in content.lines() {
        let mut quotes: Vec<&str> = Vec::new();
        let mut rest = line;
        while let Some(start) = rest.find('"') {
            rest = &rest[start + 1..];
            if let Some(end) = rest.find('"') {
                quotes.push(&rest[..end]);
                rest = &rest[end + 1..];
            } else {
                break;
            }
        }
        if quotes.len() >= 2 && quotes[0].to_ascii_lowercase() == key_lower {
            return Some(quotes[1].to_string());
        }
    }
    None
}

pub fn parse_steam_library_folders(steam_root: &str, vdf_content: &str) -> Vec<String> {
    let mut seen: HashSet<String> = HashSet::new();
    let mut out = Vec::new();

    let norm_root = normalize_vdf_path(steam_root);
    if !norm_root.is_empty() && seen.insert(norm_root.to_ascii_lowercase()) {
        out.push(norm_root);
    }

    for line in vdf_content.lines() {
        let mut quotes: Vec<&str> = Vec::new();
        let mut rest = line;
        while let Some(start) = rest.find('"') {
            rest = &rest[start + 1..];
            if let Some(end) = rest.find('"') {
                quotes.push(&rest[..end]);
                rest = &rest[end + 1..];
            } else {
                break;
            }
        }
        if quotes.len() >= 2 {
            let k = quotes[0].trim();
            let is_path_key = k.eq_ignore_ascii_case("path")
                || (!k.is_empty() && k.chars().all(|c| c.is_ascii_digit()));
            if is_path_key {
                let candidate = normalize_vdf_path(quotes[1]);
                if (candidate.contains(':') || candidate.starts_with(r"\\"))
                    && seen.insert(candidate.to_ascii_lowercase())
                {
                    out.push(candidate);
                }
            }
        }
    }

    out
}

fn clean_alnum_lower(s: &str) -> String {
    s.chars()
        .filter(|c| c.is_ascii_alphanumeric())
        .map(|c| c.to_ascii_lowercase())
        .collect()
}

fn has_mismatching_trailing_number(candidate_clean: &str, target_clean: &str) -> bool {
    if candidate_clean.len() <= target_clean.len() {
        return false;
    }
    if let Some(idx) = candidate_clean.find(target_clean) {
        let after_idx = idx + target_clean.len();
        if let Some(ch) = candidate_clean[after_idx..].chars().next() {
            return ch.is_ascii_digit();
        }
    }
    false
}

pub fn pick_best_steam_exe(
    game_dir: &Path,
    candidates: &[PathBuf],
    install_dir: &str,
    game_name: &str,
) -> Option<PathBuf> {
    let valid: Vec<&PathBuf> = candidates
        .iter()
        .filter(|c| {
            let stem = c
                .file_stem()
                .and_then(|s| s.to_str())
                .unwrap_or("")
                .to_ascii_lowercase();
            !IGNORED_EXE_PREFIXES
                .iter()
                .any(|prefix| stem.starts_with(prefix))
        })
        .collect();

    if valid.is_empty() {
        return None;
    }

    let clean_install = clean_alnum_lower(install_dir);
    let clean_name = clean_alnum_lower(game_name);

    // Priority 1: Exact clean name match
    for cand in &valid {
        let stem = cand.file_stem().and_then(|s| s.to_str()).unwrap_or("");
        let clean_fn = clean_alnum_lower(stem);
        if (!clean_install.is_empty() && clean_fn == clean_install)
            || (!clean_name.is_empty() && clean_fn == clean_name)
        {
            return Some((*cand).clone());
        }
    }

    // Priority 2: Substring match guarding trailing sequel number (portal vs portal2)
    for cand in &valid {
        let stem = cand.file_stem().and_then(|s| s.to_str()).unwrap_or("");
        let clean_fn = clean_alnum_lower(stem);
        if !clean_install.is_empty()
            && clean_fn.contains(&clean_install)
            && !has_mismatching_trailing_number(&clean_fn, &clean_install)
        {
            return Some((*cand).clone());
        }
        if !clean_name.is_empty()
            && clean_fn.contains(&clean_name)
            && !has_mismatching_trailing_number(&clean_fn, &clean_name)
        {
            return Some((*cand).clone());
        }
    }

    // Priority 3: Root candidates (prefer sole hl2.exe, else largest file)
    let root_cands: Vec<&PathBuf> = valid
        .iter()
        .copied()
        .filter(|c| {
            c.parent()
                .map(|p| p.to_string_lossy().eq_ignore_ascii_case(&game_dir.to_string_lossy()))
                .unwrap_or(false)
        })
        .collect();

    if !root_cands.is_empty() {
        if root_cands.len() == 1
            && root_cands[0]
                .file_name()
                .and_then(|s| s.to_str())
                .map(|s| s.eq_ignore_ascii_case("hl2.exe"))
                .unwrap_or(false)
        {
            return Some(root_cands[0].clone());
        }
        return root_cands
            .into_iter()
            .max_by_key(|p| fs::metadata(p).map(|m| m.len()).unwrap_or(0))
            .cloned();
    }

    valid
        .into_iter()
        .max_by_key(|p| fs::metadata(p).map(|m| m.len()).unwrap_or(0))
        .cloned()
}

fn collect_exes_up_to_depth2(dir: &Path) -> Vec<PathBuf> {
    let mut out = Vec::new();
    let Ok(entries) = fs::read_dir(dir) else {
        return out;
    };
    let mut subdirs1 = Vec::new();
    for entry in entries.flatten() {
        let p = entry.path();
        if p.is_file() {
            if p.extension()
                .and_then(|e| e.to_str())
                .map(|e| e.eq_ignore_ascii_case("exe"))
                .unwrap_or(false)
            {
                out.push(p);
            }
        } else if p.is_dir() {
            subdirs1.push(p);
        }
    }

    for sub1 in subdirs1 {
        let Ok(e1) = fs::read_dir(&sub1) else {
            continue;
        };
        let mut subdirs2 = Vec::new();
        for entry in e1.flatten() {
            let p = entry.path();
            if p.is_file() {
                if p.extension()
                    .and_then(|e| e.to_str())
                    .map(|e| e.eq_ignore_ascii_case("exe"))
                    .unwrap_or(false)
                {
                    out.push(p);
                }
            } else if p.is_dir() {
                subdirs2.push(p);
            }
        }
        for sub2 in subdirs2 {
            let Ok(e2) = fs::read_dir(&sub2) else {
                continue;
            };
            for entry in e2.flatten() {
                let p = entry.path();
                if p.is_file()
                    && p.extension()
                        .and_then(|e| e.to_str())
                        .map(|e| e.eq_ignore_ascii_case("exe"))
                        .unwrap_or(false)
                {
                    out.push(p);
                }
            }
        }
    }
    out
}

pub fn parse_steam_app_manifest(content: &str, steamapps_dir: &Path) -> Option<DetectedGameInfo> {
    let app_id = extract_vdf_value(content, "appid");
    let name = extract_vdf_value(content, "name")?;
    let install_dir = extract_vdf_value(content, "installdir")?;

    if name.trim().is_empty() || install_dir.trim().is_empty() {
        return None;
    }
    if let Some(ref aid) = app_id {
        if STEAM_IGNORED_APP_IDS
            .iter()
            .any(|ignored| aid.eq_ignore_ascii_case(ignored))
        {
            return None;
        }
    }
    let name_lower = name.to_ascii_lowercase();
    if name_lower.contains("steamworks")
        || name_lower.contains("proton")
        || name_lower.contains("redistributable")
    {
        return None;
    }

    let game_dir = steamapps_dir.join("common").join(&install_dir);
    if !game_dir.is_dir() {
        return None;
    }
    let cands = collect_exes_up_to_depth2(&game_dir);
    let main_exe = pick_best_steam_exe(&game_dir, &cands, &install_dir, &name)?;

    Some(DetectedGameInfo {
        name: name.trim().to_string(),
        executable_path: main_exe.to_string_lossy().to_string(),
        launcher: "Steam".to_string(),
        app_id,
        icon_path: None,
        aum_id: None,
    })
}

fn default_steam_root() -> Option<String> {
    if let Some(p) = read_reg_sz(HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath") {
        let norm = normalize_vdf_path(&p);
        if Path::new(&norm).is_dir() {
            return Some(norm);
        }
    }
    if let Some(p) = read_reg_sz(HKEY_LOCAL_MACHINE, r"Software\Valve\Steam", "InstallPath") {
        let norm = normalize_vdf_path(&p);
        if Path::new(&norm).is_dir() {
            return Some(norm);
        }
    }
    for fallback in [r"C:\Program Files (x86)\Steam", r"C:\Program Files\Steam"] {
        if Path::new(fallback).is_dir() {
            return Some(fallback.to_string());
        }
    }
    None
}

pub fn detect_steam_games() -> Vec<DetectedGameInfo> {
    let mut out = Vec::new();
    let Some(steam_root) = default_steam_root() else {
        return out;
    };
    let vdf_path = Path::new(&steam_root)
        .join("steamapps")
        .join("libraryfolders.vdf");
    let vdf_content = fs::read_to_string(&vdf_path).unwrap_or_default();
    let libraries = parse_steam_library_folders(&steam_root, &vdf_content);

    for lib in libraries {
        let steamapps = Path::new(&lib).join("steamapps");
        if !steamapps.is_dir() {
            continue;
        }
        let Ok(entries) = fs::read_dir(&steamapps) else {
            continue;
        };
        for entry in entries.flatten() {
            let p = entry.path();
            let fname = p
                .file_name()
                .and_then(|s| s.to_str())
                .unwrap_or("")
                .to_ascii_lowercase();
            if fname.starts_with("appmanifest_") && fname.ends_with(".acf") {
                if let Ok(content) = fs::read_to_string(&p) {
                    if let Some(g) = parse_steam_app_manifest(&content, &steamapps) {
                        out.push(g);
                    }
                }
            }
        }
    }
    out
}

// --- Epic Games ---

pub fn parse_epic_manifest_json(json_str: &str, check_exists: bool) -> Option<DetectedGameInfo> {
    let v: Value = serde_json::from_str(json_str.trim_start_matches('\u{feff}')).ok()?;
    let display_name = v.get("DisplayName")?.as_str()?.trim();
    let install_loc = v.get("InstallLocation")?.as_str()?.trim();
    let launch_exe = v.get("LaunchExecutable")?.as_str()?.trim();
    let app_name = v
        .get("AppName")
        .and_then(|s| s.as_str())
        .map(|s| s.to_string());

    if display_name.is_empty() || install_loc.is_empty() || launch_exe.is_empty() {
        return None;
    }
    let dn_lower = display_name.to_ascii_lowercase();
    if dn_lower.contains("epic games") || dn_lower.contains("unreal engine") {
        return None;
    }

    let full_path = Path::new(install_loc).join(launch_exe);
    if check_exists && !full_path.is_file() {
        return None;
    }

    Some(DetectedGameInfo {
        name: display_name.to_string(),
        executable_path: full_path.to_string_lossy().to_string(),
        launcher: "Epic".to_string(),
        app_id: app_name,
        icon_path: None,
        aum_id: None,
    })
}

pub fn detect_epic_games(manifests_dir_override: Option<&Path>) -> Vec<DetectedGameInfo> {
    let mut out = Vec::new();
    let dir: PathBuf = match manifests_dir_override {
        Some(d) => d.to_path_buf(),
        None => {
            let prog_data = std::env::var("ProgramData")
                .unwrap_or_else(|_| r"C:\ProgramData".to_string());
            Path::new(&prog_data).join(r"Epic\EpicGamesLauncher\Data\Manifests")
        }
    };
    if !dir.is_dir() {
        return out;
    }
    let Ok(entries) = fs::read_dir(&dir) else {
        return out;
    };
    for entry in entries.flatten() {
        let p = entry.path();
        if p.extension()
            .and_then(|e| e.to_str())
            .map(|e| e.eq_ignore_ascii_case("item"))
            .unwrap_or(false)
        {
            if let Ok(raw) = fs::read_to_string(&p) {
                if let Some(g) = parse_epic_manifest_json(&raw, true) {
                    out.push(g);
                }
            }
        }
    }
    out
}

// --- Xbox / Microsoft Store ---

pub fn parse_gaming_root_bytes(bytes: &[u8]) -> Vec<String> {
    let mut chars = String::new();
    for &b in bytes {
        if b == 0 {
            continue;
        }
        if b == b'\r' || b == b'\n' || (b >= 32 && b < 127) {
            chars.push(b as char);
        }
    }
    let stripped = chars.strip_prefix("RGBX").unwrap_or(&chars);
    stripped
        .lines()
        .map(|l| l.trim().trim_matches(|c| c == '\\' || c == '/'))
        .filter(|l| !l.is_empty())
        .map(|l| l.to_string())
        .collect()
}

fn extract_xml_attr(xml: &str, tag_name: &str, attr_name: &str) -> Option<String> {
    let xml_lower = xml.to_ascii_lowercase();
    let tag_needle = format!("<{}", tag_name.to_ascii_lowercase());
    let attr_needle = format!("{}=", attr_name.to_ascii_lowercase());

    let mut search_from = 0usize;
    while let Some(pos) = xml_lower[search_from..].find(&tag_needle) {
        let tag_start = search_from + pos;
        let tag_end = xml_lower[tag_start..].find('>')?;
        let tag_slice_orig = &xml[tag_start..tag_start + tag_end + 1];
        let tag_slice_lower = &xml_lower[tag_start..tag_start + tag_end + 1];

        if let Some(apos) = tag_slice_lower.find(&attr_needle) {
            let after_eq = &tag_slice_orig[apos + attr_needle.len()..].trim_start();
            if let Some(quote) = after_eq.chars().next().filter(|&c| c == '"' || c == '\'') {
                let inner = &after_eq[1..];
                if let Some(end_q) = inner.find(quote) {
                    return Some(inner[..end_q].to_string());
                }
            }
        }
        search_from = tag_start + tag_end + 1;
    }
    None
}

fn extract_attr_from_tag(tag_orig: &str, tag_lower: &str, attr_name: &str) -> Option<String> {
    let attr_needle = format!("{}=", attr_name.to_ascii_lowercase());
    let apos = tag_lower.find(&attr_needle)?;
    let after_eq = tag_orig[apos + attr_needle.len()..].trim_start();
    let quote = after_eq.chars().next().filter(|&c| c == '"' || c == '\'')?;
    let inner = &after_eq[1..];
    let end_q = inner.find(quote)?;
    Some(inner[..end_q].to_string())
}

/// Exact port of C# XboxAndStoreGameDetector MicrosoftGame.config <ExecutableList> selection:
/// filters TargetDeviceFamily (omitted | "PC" | "Desktop"), skips IgnoredExeNames, and preserves fallback helper exe.
pub fn select_microsoft_game_config_exe(xml: &str) -> (Option<String>, Option<String>) {
    let xml_lower = xml.to_ascii_lowercase();
    let mut search_from = 0usize;
    let mut primary: Option<String> = None;
    let mut fallback_helper: Option<String> = None;

    while let Some(pos) = xml_lower[search_from..].find("<executable") {
        let tag_start = search_from + pos;
        let Some(tag_end_rel) = xml_lower[tag_start..].find('>') else {
            break;
        };
        let after_name = xml_lower.as_bytes().get(tag_start + 11).copied().unwrap_or(b' ');
        // Distinguish <Executable ...> from <ExecutableList>
        if after_name == b' ' || after_name == b'\t' || after_name == b'\r' || after_name == b'\n' || after_name == b'/' || after_name == b'>' {
            let tag_orig = &xml[tag_start..tag_start + tag_end_rel + 1];
            let tag_lower = &xml_lower[tag_start..tag_start + tag_end_rel + 1];

            let target_device = extract_attr_from_tag(tag_orig, tag_lower, "TargetDeviceFamily")
                .unwrap_or_default();
            let device_ok = target_device.is_empty()
                || target_device.eq_ignore_ascii_case("PC")
                || target_device.eq_ignore_ascii_case("Desktop");

            if device_ok {
                if let Some(cand) = extract_attr_from_tag(tag_orig, tag_lower, "Name") {
                    if !cand.trim().is_empty() {
                        let fname = Path::new(&cand)
                            .file_name()
                            .and_then(|s| s.to_str())
                            .unwrap_or(&cand)
                            .to_ascii_lowercase();
                        if !XBOX_IGNORED_EXE_NAMES.iter().any(|&ign| ign == fname) {
                            primary = Some(cand);
                            break;
                        } else if fallback_helper.is_none() {
                            fallback_helper = Some(cand);
                        }
                    }
                }
            }
        }
        search_from = tag_start + tag_end_rel + 1;
    }

    (primary, fallback_helper)
}

pub fn parse_xbox_game_folder(game_folder: &Path) -> Option<DetectedGameInfo> {
    if !game_folder.is_dir() {
        return None;
    }
    let folder_name = game_folder.file_name()?.to_str()?;
    if folder_name.eq_ignore_ascii_case("GameSave") {
        return None;
    }

    let content_dir = game_folder.join("Content");
    let search_dir = if content_dir.is_dir() {
        content_dir.as_path()
    } else {
        game_folder
    };

    let mut game_name: Option<String> = None;
    let mut exe_name: Option<String> = None;
    let mut fallback_helper_exe: Option<String> = None;
    let mut app_id: Option<String> = None;
    let mut has_manifest = false;

    let mgc_path = if search_dir.join("MicrosoftGame.config").is_file() {
        Some(search_dir.join("MicrosoftGame.config"))
    } else if game_folder.join("MicrosoftGame.config").is_file() {
        Some(game_folder.join("MicrosoftGame.config"))
    } else {
        None
    };

    if let Some(ref p) = mgc_path {
        has_manifest = true;
        if let Ok(xml) = fs::read_to_string(p) {
            app_id = extract_xml_attr(&xml, "Identity", "Name");
            game_name = extract_xml_attr(&xml, "ShellVisuals", "DefaultDisplayName")
                .or_else(|| extract_xml_attr(&xml, "ShellVisuals", "DisplayName"));
            let (prim, helper) = select_microsoft_game_config_exe(&xml);
            exe_name = prim;
            fallback_helper_exe = helper;
        }
    }

    let appx_path = if search_dir.join("appxmanifest.xml").is_file() {
        Some(search_dir.join("appxmanifest.xml"))
    } else if game_folder.join("appxmanifest.xml").is_file() {
        Some(game_folder.join("appxmanifest.xml"))
    } else {
        None
    };

    if let Some(ref p) = appx_path {
        has_manifest = true;
        if let Ok(xml) = fs::read_to_string(p) {
            if app_id.is_none() {
                app_id = extract_xml_attr(&xml, "Identity", "Name");
            }
            if let Some(cand) = extract_xml_attr(&xml, "Application", "Executable") {
                let fname = Path::new(&cand)
                    .file_name()
                    .and_then(|s| s.to_str())
                    .unwrap_or(&cand)
                    .to_ascii_lowercase();
                if exe_name.is_none() && !XBOX_IGNORED_EXE_NAMES.iter().any(|&ign| ign == fname) {
                    exe_name = Some(cand);
                } else if fallback_helper_exe.is_none() {
                    fallback_helper_exe = Some(cand);
                }
            }
            if game_name
                .as_deref()
                .map(|s| s.is_empty() || s.to_ascii_lowercase().starts_with("ms-resource:"))
                .unwrap_or(true)
            {
                if let Some(dn) = extract_xml_attr(&xml, "VisualElements", "DisplayName") {
                    if !dn.to_ascii_lowercase().starts_with("ms-resource:") {
                        game_name = Some(dn);
                    }
                }
            }
        }
    }

    let folder_lower = game_folder.to_string_lossy().to_ascii_lowercase();
    let is_explicit_xbox =
        folder_lower.contains("xboxgames") || folder_lower.contains("modifiablewindowsapps");
    if !has_manifest && !is_explicit_xbox {
        return None;
    }

    let mut resolved_exe: Option<PathBuf> = None;
    if let Some(ref rel) = exe_name {
        let norm = rel.replace('/', r"\").trim_start_matches('\\').to_string();
        for base in [search_dir, game_folder] {
            let cand = base.join(&norm);
            if cand.is_file() {
                resolved_exe = Some(cand);
                break;
            }
        }
    }
    if resolved_exe.is_none() {
        let exes = collect_exes_up_to_depth2(search_dir);
        resolved_exe = exes.into_iter().find(|p| {
            let fname = p
                .file_name()
                .and_then(|s| s.to_str())
                .unwrap_or("")
                .to_ascii_lowercase();
            !XBOX_IGNORED_EXE_NAMES.iter().any(|&ign| ign == fname)
        });
    }
    if resolved_exe.is_none() {
        if let Some(ref helper_rel) = fallback_helper_exe {
            let norm = helper_rel.replace('/', r"\").trim_start_matches('\\').to_string();
            for base in [search_dir, game_folder] {
                let cand = base.join(&norm);
                if cand.is_file() {
                    resolved_exe = Some(cand);
                    break;
                }
            }
        }
    }

    let final_exe = resolved_exe?;
    let mut final_name = game_name
        .filter(|s| !s.trim().is_empty() && !s.to_ascii_lowercase().starts_with("ms-resource:"))
        .unwrap_or_else(|| folder_name.to_string());

    if let Some(ref aid) = app_id {
        if let Some(known) = known_display_name(aid) {
            if final_name.eq_ignore_ascii_case(aid) || final_name == folder_name {
                final_name = known.to_string();
            }
        }
    }

    Some(DetectedGameInfo {
        name: final_name.trim().to_string(),
        executable_path: final_exe.to_string_lossy().to_string(),
        launcher: "Xbox".to_string(),
        app_id,
        icon_path: None,
        aum_id: None,
    })
}

fn enumerate_drive_roots() -> Vec<String> {
    let mask = unsafe { GetLogicalDrives() };
    let mut drives = Vec::new();
    for i in 0..26u32 {
        if (mask & (1 << i)) != 0 {
            let letter = (b'A' + i as u8) as char;
            drives.push(format!("{}:\\", letter));
        }
    }
    drives
}

fn scan_game_config_store_registry() -> Vec<DetectedGameInfo> {
    let mut out = Vec::new();
    let subkeys = enum_reg_subkeys(HKEY_CURRENT_USER, r"System\GameConfigStore\Children");
    for sub in subkeys {
        let full_sub = format!(r"System\GameConfigStore\Children\{}", sub);
        let Some(matched_exe) = read_reg_sz(HKEY_CURRENT_USER, &full_sub, "MatchedExeFullPath")
        else {
            continue;
        };
        let trimmed = matched_exe.trim().trim_matches('"').trim();
        if trimmed.is_empty() || !Path::new(trimmed).is_file() {
            continue;
        }
        let lower = trimmed.to_ascii_lowercase();
        let exe_name = Path::new(trimmed)
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();
        if XBOX_IGNORED_EXE_NAMES.iter().any(|&ign| ign == exe_name) {
            continue;
        }
        if lower.contains(r"\steamapps\")
            || lower.contains(r"\epic games\")
            || lower.contains(r"\windows\system32\")
            || lower.contains(r"\windows\syswow64\")
            || lower.contains(r"\windows\systemapps\")
            || lower.contains(r"\program files\windowsapps\microsoft.windows.")
            || lower.contains(r"\google\chrome\")
            || lower.contains(r"\mozilla firefox\")
            || lower.contains(r"\discord\")
            || lower.contains(r"\notiglow\")
            || lower.contains(r"\microsoft\onedrive\")
        {
            continue;
        }
        let title = read_reg_sz(HKEY_CURRENT_USER, &full_sub, "Title")
            .filter(|s| !s.trim().is_empty())
            .unwrap_or_else(|| {
                Path::new(trimmed)
                    .file_stem()
                    .and_then(|s| s.to_str())
                    .unwrap_or(trimmed)
                    .to_string()
            });
        let launcher = if lower.contains("xbox") || lower.contains("gamingroot") {
            "Xbox"
        } else {
            "Microsoft Store"
        };
        out.push(DetectedGameInfo {
            name: title.trim().to_string(),
            executable_path: trimmed.to_string(),
            launcher: launcher.to_string(),
            app_id: Some(sub),
            icon_path: None,
            aum_id: None,
        });
    }
    out
}

/// Discovers installed (including unlaunched) Microsoft Store / Xbox packages via the current user's
/// AppModel Repository registry (`HKCU\...\AppModel\Repository\Packages`), matching C# ScanPackageStoreGames().
fn scan_appmodel_store_packages() -> Vec<DetectedGameInfo> {
    let mut out = Vec::new();
    let repo_root = r"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
    let subkeys = enum_reg_subkeys(HKEY_CURRENT_USER, repo_root);

    for pkg_full_name in subkeys {
        let pkg_name = pkg_full_name.split('_').next().unwrap_or(&pkg_full_name);
        if EXCLUDED_PACKAGE_PREFIXES
            .iter()
            .any(|p| pkg_name.len() >= p.len() && pkg_name[..p.len()].eq_ignore_ascii_case(p))
        {
            continue;
        }

        let sub_path = format!(r"{}\{}", repo_root, pkg_full_name);
        let Some(root_folder) = read_reg_sz(HKEY_CURRENT_USER, &sub_path, "PackageRootFolder") else {
            continue;
        };
        let lower_root = root_folder.to_ascii_lowercase();
        if lower_root.contains(r"\windows\systemapps\") || lower_root.contains(r"\windows\system32\") {
            continue;
        }

        let root_path = Path::new(&root_folder);
        let is_known = known_display_name(pkg_name).is_some();
        let has_mgc = root_path.join("MicrosoftGame.config").is_file()
            || root_path.join("Content").join("MicrosoftGame.config").is_file();
        let mut has_game_manifest = false;
        if !is_known && !has_mgc {
            let appx = root_path.join("appxmanifest.xml");
            if let Ok(xml) = fs::read_to_string(&appx) {
                let xl = xml.to_ascii_lowercase();
                if xl.contains("category=\"game\"")
                    || xl.contains("category=\"games\"")
                    || xl.contains("xboxlive")
                {
                    has_game_manifest = true;
                }
            }
        }
        if !is_known && !has_mgc && !has_game_manifest {
            continue;
        }

        let reg_disp = read_reg_sz(HKEY_CURRENT_USER, &sub_path, "DisplayName")
            .filter(|d| !d.trim().is_empty() && !d.starts_with("@{") && !d.to_ascii_lowercase().starts_with("ms-resource:"));

        if let Some(mut parsed) = parse_xbox_game_folder(root_path) {
            if let Some(dn) = reg_disp {
                if parsed.name.eq_ignore_ascii_case(pkg_name) {
                    parsed.name = dn;
                }
            }
            if parsed.app_id.is_none() {
                parsed.app_id = Some(pkg_name.to_string());
            }
            out.push(parsed);
        } else {
            let disp = known_display_name(pkg_name)
                .map(|s| s.to_string())
                .or(reg_disp)
                .unwrap_or_else(|| pkg_name.to_string());
            let launcher = if pkg_name.to_ascii_lowercase().contains("xbox")
                || pkg_name.to_ascii_lowercase().contains("gaming")
                || is_known
            {
                "Xbox"
            } else {
                "Microsoft Store"
            };
            out.push(DetectedGameInfo {
                name: disp,
                executable_path: format!("{}.exe", pkg_name),
                launcher: launcher.to_string(),
                app_id: Some(pkg_name.to_string()),
                icon_path: None,
                aum_id: None,
            });
        }
    }
    out
}

pub fn detect_xbox_and_store_games() -> Vec<DetectedGameInfo> {
    let mut out = Vec::new();
    for drive in enumerate_drive_roots() {
        let mut roots = vec![
            Path::new(&drive).join("XboxGames"),
            Path::new(&drive).join("ModifiableWindowsApps"),
        ];
        let gr = Path::new(&drive).join(".GamingRoot");
        if let Ok(bytes) = fs::read(&gr) {
            for rel in parse_gaming_root_bytes(&bytes) {
                let p = if rel.contains(':') {
                    PathBuf::from(rel)
                } else {
                    Path::new(&drive).join(rel)
                };
                if !roots.iter().any(|r| r.to_string_lossy().eq_ignore_ascii_case(&p.to_string_lossy())) {
                    roots.push(p);
                }
            }
        }
        for root in roots {
            if !root.is_dir() {
                continue;
            }
            if let Ok(entries) = fs::read_dir(&root) {
                for entry in entries.flatten() {
                    let p = entry.path();
                    if p.is_dir() {
                        if let Some(g) = parse_xbox_game_folder(&p) {
                            out.push(g);
                        }
                    }
                }
            }
        }
    }
    out.extend(scan_game_config_store_registry());
    out.extend(scan_appmodel_store_packages());
    out
}

// --- Win32 Registry Helpers ---

fn to_wide_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn read_reg_sz(root: HKEY, subkey: &str, value_name: &str) -> Option<String> {
    unsafe {
        let sub_w = to_wide_null(subkey);
        let mut hkey = HKEY::default();
        if RegOpenKeyExW(root, PCWSTR(sub_w.as_ptr()), 0, KEY_READ, &mut hkey) != ERROR_SUCCESS {
            return None;
        }
        let val_w = to_wide_null(value_name);
        let mut reg_type = REG_VALUE_TYPE(0);
        let mut buf_bytes = 0u32;
        let q1 = RegQueryValueExW(
            hkey,
            PCWSTR(val_w.as_ptr()),
            None,
            Some(&mut reg_type),
            None,
            Some(&mut buf_bytes),
        );
        if q1 != ERROR_SUCCESS || buf_bytes < 2 || reg_type != REG_SZ {
            let _ = RegCloseKey(hkey);
            return None;
        }
        let mut buf = vec![0u16; (buf_bytes as usize / 2) + 1];
        let q2 = RegQueryValueExW(
            hkey,
            PCWSTR(val_w.as_ptr()),
            None,
            Some(&mut reg_type),
            Some(buf.as_mut_ptr() as *mut u8),
            Some(&mut buf_bytes),
        );
        let _ = RegCloseKey(hkey);
        if q2 != ERROR_SUCCESS {
            return None;
        }
        let end = buf.iter().position(|&c| c == 0).unwrap_or(buf.len());
        Some(String::from_utf16_lossy(&buf[..end]))
    }
}

fn enum_reg_subkeys(root: HKEY, subkey: &str) -> Vec<String> {
    let mut out = Vec::new();
    unsafe {
        let sub_w = to_wide_null(subkey);
        let mut hkey = HKEY::default();
        if RegOpenKeyExW(root, PCWSTR(sub_w.as_ptr()), 0, KEY_READ, &mut hkey) != ERROR_SUCCESS {
            return out;
        }
        let mut idx = 0u32;
        loop {
            let mut name_buf = [0u16; 256];
            let mut name_len = name_buf.len() as u32;
            let res = RegEnumKeyExW(
                hkey,
                idx,
                PWSTR(name_buf.as_mut_ptr()),
                &mut name_len,
                None,
                PWSTR::null(),
                None,
                None,
            );
            if res != ERROR_SUCCESS {
                break;
            }
            out.push(String::from_utf16_lossy(&name_buf[..name_len as usize]));
            idx += 1;
        }
        let _ = RegCloseKey(hkey);
    }
    out
}

fn now_unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

#[cfg(test)]
mod tests {
    use super::*;

    struct MockForegroundProvider {
        current: Mutex<Option<ForegroundProcessInfo>>,
    }

    impl MockForegroundProvider {
        fn new(fg: Option<ForegroundProcessInfo>) -> Self {
            Self {
                current: Mutex::new(fg),
            }
        }
        fn set(&self, fg: Option<ForegroundProcessInfo>) {
            *self.current.lock().unwrap() = fg;
        }
    }

    impl ForegroundProcessProvider for MockForegroundProvider {
        fn get_foreground_process(&self) -> Option<ForegroundProcessInfo> {
            self.current.lock().unwrap().clone()
        }
    }

    #[test]
    fn test_game_detected_closed_dedup_and_multi_game_priority() {
        let detector = GameDetectorState::new();
        let mut settings = AppSettings::default();
        settings.GamingModeEnabled = true;
        settings.TrackedGames = vec![
            r"C:\Games\CS2\cs2.exe".to_string(),
            r"D:\Games\Cyberpunk\Cyberpunk2077.exe".to_string(),
        ];

        let prov = MockForegroundProvider::new(Some(ForegroundProcessInfo {
            pid: 1111,
            process_name: "cs2".to_string(),
            full_path: Some(r"C:\Games\CS2\cs2.exe".to_string()),
            window_title: "Counter-Strike 2".to_string(),
        }));

        // 1. First detection emits GAME_DETECTED
        let g1 = detector.poll_foreground_with(&prov, &settings);
        assert_eq!(g1.as_ref().unwrap().identifier, "cs2.exe");
        assert_eq!(detector.total_detections.load(Ordering::Relaxed), 1);

        // 2. Repeated polls with same game must NOT duplicate GAME_DETECTED
        let _ = detector.poll_foreground_with(&prov, &settings);
        let _ = detector.poll_foreground_with(&prov, &settings);
        assert_eq!(detector.total_detections.load(Ordering::Relaxed), 1);

        // 3. Foreground switches to second running game -> closes CS2, detects Cyberpunk2077
        prov.set(Some(ForegroundProcessInfo {
            pid: 2222,
            process_name: "Cyberpunk2077".to_string(),
            full_path: Some(r"D:\Games\Cyberpunk\Cyberpunk2077.exe".to_string()),
            window_title: "Cyberpunk 2077".to_string(),
        }));
        let g2 = detector.poll_foreground_with(&prov, &settings);
        assert_eq!(g2.as_ref().unwrap().identifier, "cyberpunk2077.exe");
        assert_eq!(detector.total_closes.load(Ordering::Relaxed), 1);
        assert_eq!(detector.total_detections.load(Ordering::Relaxed), 2);

        // 4. Game closes / non-game window gets focus -> emits GAME_CLOSED
        prov.set(Some(ForegroundProcessInfo {
            pid: 3333,
            process_name: "notepad".to_string(),
            full_path: Some(r"C:\Windows\notepad.exe".to_string()),
            window_title: "Untitled - Notepad".to_string(),
        }));
        let g3 = detector.poll_foreground_with(&prov, &settings);
        assert!(g3.is_none());
        assert_eq!(detector.total_closes.load(Ordering::Relaxed), 2);

        let (evs, _) = detector.drain_events(0, 16);
        let kinds: Vec<&str> = evs.iter().map(|e| e.kind.as_str()).collect();
        assert_eq!(
            kinds,
            vec!["GAME_DETECTED", "GAME_CLOSED", "GAME_DETECTED", "GAME_CLOSED"]
        );
    }

    #[test]
    fn test_steam_vdf_and_epic_and_xbox_metadata_parsing() {
        // Steam libraryfolders.vdf (modern + legacy)
        let vdf = r#"
        "libraryfolders"
        {
            "0"
            {
                "path" "C:\\Program Files (x86)\\Steam"
            }
            "1" "D:\\SteamLibrary"
        }
        "#;
        let libs = parse_steam_library_folders(r"C:\Program Files (x86)\Steam", vdf);
        assert_eq!(libs.len(), 2);
        assert_eq!(libs[1], r"D:\SteamLibrary");

        // Steam sequel guard: "portal" must not match "portal2.exe"
        let dir = Path::new(r"C:\Steam\steamapps\common\Portal");
        let cands = vec![
            dir.join("portal2.exe"),
            dir.join("portal.exe"),
            dir.join("unins000.exe"),
        ];
        let picked = pick_best_steam_exe(dir, &cands, "Portal", "Portal").unwrap();
        assert_eq!(picked.file_name().unwrap(), "portal.exe");

        // Epic .item JSON parsing
        let epic_json = r#"{
            "DisplayName": "Rocket League",
            "InstallLocation": "D:\\EpicGames\\rocketleague",
            "LaunchExecutable": "Binaries/Win64/RocketLeague.exe",
            "AppName": "Sugar"
        }"#;
        let epic = parse_epic_manifest_json(epic_json, false).unwrap();
        assert_eq!(epic.name, "Rocket League");
        assert_eq!(epic.launcher, "Epic");
        assert_eq!(epic.app_id.as_deref(), Some("Sugar"));

        // Epic excludes launcher/UE
        let epic_ue = r#"{
            "DisplayName": "Unreal Engine 5.4",
            "InstallLocation": "C:\\UE",
            "LaunchExecutable": "UE.exe"
        }"#;
        assert!(parse_epic_manifest_json(epic_ue, false).is_none());

        // Xbox .GamingRoot binary parsing
        let mut gr_bytes = b"RGBX\x01\x00\x00\x00".to_vec();
        for ch in "XboxGames\r\n".encode_utf16() {
            gr_bytes.extend_from_slice(&ch.to_le_bytes());
        }
        let roots = parse_gaming_root_bytes(&gr_bytes);
        assert_eq!(roots, vec!["XboxGames".to_string()]);

        // Xbox MicrosoftGame.config XML attribute extraction
        let xml = r#"<Game><Identity Name="Microsoft.254428597CFE2"/><ShellVisuals DefaultDisplayName="Halo Infinite"/><ExecutableList><Executable Name="HaloInfinite.exe" TargetDeviceFamily="PC"/></ExecutableList></Game>"#;
        assert_eq!(
            extract_xml_attr(xml, "Identity", "Name").as_deref(),
            Some("Microsoft.254428597CFE2")
        );
        assert_eq!(
            extract_xml_attr(xml, "ShellVisuals", "DefaultDisplayName").as_deref(),
            Some("Halo Infinite")
        );
        assert_eq!(
            extract_xml_attr(xml, "Executable", "Name").as_deref(),
            Some("HaloInfinite.exe")
        );

        // Multi-Executable MicrosoftGame.config: skips Console target and IgnoredExeNames
        let multi_xml = r#"<Game>
            <ExecutableList>
                <Executable Name="ConsoleOnly.exe" TargetDeviceFamily="XboxOne"/>
                <Executable Name="gamelaunchhelper.exe" TargetDeviceFamily="PC"/>
                <Executable Name="Binaries/Win64/ActualGame.exe" TargetDeviceFamily="Desktop"/>
            </ExecutableList>
        </Game>"#;
        let (prim, helper) = select_microsoft_game_config_exe(multi_xml);
        assert_eq!(prim.as_deref(), Some("Binaries/Win64/ActualGame.exe"));
        assert_eq!(helper.as_deref(), Some("gamelaunchhelper.exe"));
    }
}
