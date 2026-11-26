use std::collections::{HashMap, HashSet, VecDeque};
use std::sync::atomic::Ordering;
use std::sync::Arc;
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};
use windows::ApplicationModel::Package;
use windows::Foundation::TypedEventHandler;
use windows::UI::Notifications::Management::{
    UserNotificationListener, UserNotificationListenerAccessStatus,
};
use windows::UI::Notifications::{
    NotificationKinds, UserNotification, UserNotificationChangedEventArgs,
};
use windows::Win32::System::Com::{CoInitializeEx, CoUninitialize, COINIT_MULTITHREADED};

use crate::ipc::WorkerState;

const MAX_EVENT_QUEUE: usize = 256;
const DEDUP_WINDOW_MS: u64 = 350;
const PERMISSION_RECHECK_INTERVAL_MS: u64 = 3000;

/// Explicit access state reported over IPC. Unknown is strictly separated from Allowed.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u64)]
pub enum NotifAccessState {
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
}

impl NotifAccessState {
    pub fn as_str(self) -> &'static str {
        match self {
            NotifAccessState::Allowed => "Allowed",
            NotifAccessState::Denied => "Denied",
            NotifAccessState::Unknown => "Unknown",
        }
    }

    pub fn from_u64(v: u64) -> Self {
        match v {
            1 => NotifAccessState::Allowed,
            2 => NotifAccessState::Denied,
            _ => NotifAccessState::Unknown,
        }
    }

    pub fn from_winrt(status: UserNotificationListenerAccessStatus) -> Self {
        if status == UserNotificationListenerAccessStatus::Allowed {
            NotifAccessState::Allowed
        } else if status == UserNotificationListenerAccessStatus::Denied {
            NotifAccessState::Denied
        } else {
            NotifAccessState::Unknown
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct NotificationEventRecord {
    pub seq: u64,
    pub kind: String, // "Added" | "Removed" | "AccessChanged"
    #[serde(rename = "notificationId")]
    pub notification_id: u32,
    #[serde(rename = "appId")]
    pub app_id: String,
    #[serde(rename = "appName")]
    pub app_name: String,
    #[serde(rename = "timestampUnixMs")]
    pub timestamp_unix_ms: i64,
}

#[derive(Debug, Clone)]
pub struct PolledToast {
    pub id: u32,
    pub app_id: String,
    pub app_name: String,
    pub timestamp_unix_ms: i64,
}

/// Rapid burst deduplicator (port of C# NotificationDeduplicator without storing private toast text)
pub struct NotificationDeduplicator {
    seen_ids: HashSet<u32>,
    seen_order: VecDeque<u32>,
    last_app_burst: HashMap<String, Instant>,
}

impl NotificationDeduplicator {
    pub fn new() -> Self {
        Self {
            seen_ids: HashSet::with_capacity(256),
            seen_order: VecDeque::with_capacity(256),
            last_app_burst: HashMap::with_capacity(64),
        }
    }

    pub fn seed_snapshot_id(&mut self, id: u32) {
        if self.seen_ids.insert(id) {
            self.seen_order.push_back(id);
            if self.seen_order.len() > 512 {
                if let Some(old) = self.seen_order.pop_front() {
                    self.seen_ids.remove(&old);
                }
            }
        }
    }

    /// Returns true if this notification is a duplicate and should be suppressed.
    pub fn check_and_record(&mut self, id: u32, app_id: &str, now: Instant) -> bool {
        if !self.seen_ids.insert(id) {
            return true;
        }
        self.seen_order.push_back(id);
        if self.seen_order.len() > 512 {
            if let Some(old) = self.seen_order.pop_front() {
                self.seen_ids.remove(&old);
            }
        }

        let key = app_id.trim().to_ascii_lowercase();
        if !key.is_empty() && key != "unknownapp" {
            if let Some(&prev) = self.last_app_burst.get(&key) {
                if now.duration_since(prev) < Duration::from_millis(DEDUP_WINDOW_MS) {
                    return true;
                }
            }
            if self.last_app_burst.len() > 128 {
                self.last_app_burst.clear();
            }
            self.last_app_burst.insert(key, now);
        }
        false
    }
}

pub fn now_unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

fn has_msix_package_identity() -> bool {
    Package::Current().is_ok()
}

/// Extracts strictly AppId, AppName, and CreationTime without logging or persisting private toast body text.
fn extract_safe_app_identity(n: &UserNotification) -> (String, String, i64) {
    let mut app_id = "UnknownApp".to_string();
    let mut app_name = "UnknownApp".to_string();

    if let Ok(app_info) = n.AppInfo() {
        if let Ok(aumid) = app_info.AppUserModelId() {
            let s = aumid.to_string();
            if !s.trim().is_empty() {
                app_id = s;
            }
        } else if let Ok(id) = app_info.Id() {
            let s = id.to_string();
            if !s.trim().is_empty() {
                app_id = s;
            }
        }

        if let Ok(disp) = app_info.DisplayInfo() {
            if let Ok(dn) = disp.DisplayName() {
                let s = dn.to_string();
                if !s.trim().is_empty() {
                    app_name = s;
                }
            }
        }
    }

    if app_name == "UnknownApp" && app_id != "UnknownApp" {
        app_name = app_id.clone();
    }

    let ts_ms = n
        .CreationTime()
        .ok()
        .and_then(|dt| {
            const EPOCH_DIFF_100NS: i64 = 116_444_736_000_000_000;
            if dt.UniversalTime > EPOCH_DIFF_100NS {
                Some((dt.UniversalTime - EPOCH_DIFF_100NS) / 10_000)
            } else {
                None
            }
        })
        .unwrap_or_else(now_unix_ms);

    (app_id, app_name, ts_ms)
}

pub fn push_notification_event(
    state: &Arc<WorkerState>,
    kind: &str,
    notification_id: u32,
    app_id: String,
    app_name: String,
    timestamp_unix_ms: i64,
) -> u64 {
    let seq = state.next_notif_seq.fetch_add(1, Ordering::SeqCst) + 1;
    let rec = NotificationEventRecord {
        seq,
        kind: kind.to_string(),
        notification_id,
        app_id,
        app_name,
        timestamp_unix_ms,
    };
    if let Ok(mut q) = state.notif_events.lock() {
        if q.len() >= MAX_EVENT_QUEUE {
            q.pop_front();
        }
        q.push_back(rec);
    }
    seq
}

/// Abstract backend so permission state transitions, listener lifecycle, empty lists,
/// and API errors can be deterministically verified in unit tests as well as live WinRT.
pub trait NotificationBackend {
    fn query_access(&mut self, initial_request: bool) -> NotifAccessState;
    fn start_listener_session(&mut self, state: &Arc<WorkerState>) -> bool;
    fn stop_listener_session(&mut self);
    fn fetch_toasts(&mut self) -> Result<Vec<PolledToast>, i32>;
}

/// Single-instance permission & notification monitor state machine.
/// Guarantees:
/// - Access status is checked independently and periodically (never inferred from empty toast lists).
/// - Allowed -> Denied / Unknown cleanly tears down the active listener (0 duplicate listeners/handlers).
/// - Denied / Unknown -> Allowed cleanly starts a single listener with a fresh initial snapshot.
/// - Unknown/unqueryable states are reported as Unknown (never assumed Allowed).
pub struct NotificationMonitorCore<B: NotificationBackend> {
    backend: B,
    dedup: NotificationDeduplicator,
    current_access: NotifAccessState,
    has_initialized_access: bool,
    listener_running: bool,
    active_meta: HashMap<u32, (String, String)>,
    consecutive_fetch_errors: u32,
    empty_polls: u32,
    last_permission_check: Option<Instant>,
    permission_interval: Duration,
}

impl<B: NotificationBackend> NotificationMonitorCore<B> {
    pub fn new(backend: B, permission_interval: Duration) -> Self {
        Self {
            backend,
            dedup: NotificationDeduplicator::new(),
            current_access: NotifAccessState::Unknown,
            has_initialized_access: false,
            listener_running: false,
            active_meta: HashMap::with_capacity(64),
            consecutive_fetch_errors: 0,
            empty_polls: 0,
            last_permission_check: None,
            permission_interval,
        }
    }

    pub fn is_listener_running(&self) -> bool {
        self.listener_running
    }

    pub fn current_access(&self) -> NotifAccessState {
        self.current_access
    }

    fn apply_access_transition(&mut self, state: &Arc<WorkerState>, next: NotifAccessState) {
        let prev = self.current_access;
        let changed = !self.has_initialized_access || prev != next;
        self.has_initialized_access = true;
        self.current_access = next;
        state.notif_access.store(next as u64, Ordering::SeqCst);

        if next != NotifAccessState::Allowed {
            state.notif_denied_checks.fetch_add(1, Ordering::Relaxed);
        }

        if changed {
            state.notif_access_transitions.fetch_add(1, Ordering::Relaxed);
            (state.log_cb)(&format!(
                "NotificationAccess transition: {} -> {}",
                prev.as_str(),
                next.as_str()
            ));
            push_notification_event(
                state,
                "AccessChanged",
                0,
                next.as_str().to_string(),
                prev.as_str().to_string(),
                now_unix_ms(),
            );
        }

        match next {
            NotifAccessState::Allowed => {
                // Start listener ONLY if not already running (prevents duplicate listeners/handlers!)
                if !self.listener_running {
                    let event_mode = self.backend.start_listener_session(state);
                    self.listener_running = true;
                    state.notif_event_mode.store(event_mode, Ordering::Relaxed);
                    state.active_listener_count.store(1, Ordering::SeqCst);
                    self.consecutive_fetch_errors = 0;
                    self.empty_polls = 0;
                    self.take_initial_snapshot(state);
                }
            }
            NotifAccessState::Denied | NotifAccessState::Unknown => {
                // Safely stop active listener and release event handler on permission loss
                if self.listener_running {
                    self.backend.stop_listener_session();
                    self.listener_running = false;
                    self.active_meta.clear();
                    state.notif_event_mode.store(false, Ordering::Relaxed);
                    state.active_listener_count.store(0, Ordering::SeqCst);
                }
            }
        }
    }

    fn take_initial_snapshot(&mut self, state: &Arc<WorkerState>) {
        self.active_meta.clear();
        if let Ok(toasts) = self.backend.fetch_toasts() {
            state
                .notif_history
                .store(toasts.len() as u64, Ordering::Relaxed);
            for t in toasts {
                self.dedup.seed_snapshot_id(t.id);
                self.active_meta.insert(t.id, (t.app_id, t.app_name));
            }
        }
    }

    /// Executes one monitor tick: independent periodic permission check + (if Allowed) one toast poll.
    /// Returns the recommended sleep duration in milliseconds before the next tick.
    pub fn step(&mut self, state: &Arc<WorkerState>, now: Instant) -> u64 {
        // 1. Check test-only override if set via IPC/CLI, otherwise periodic WinRT access query
        let override_val = state.test_access_override.load(Ordering::SeqCst);
        let due_for_perm_check = self
            .last_permission_check
            .map(|t| now.duration_since(t) >= self.permission_interval)
            .unwrap_or(true);

        if override_val != u64::MAX {
            let forced = NotifAccessState::from_u64(override_val);
            if !self.has_initialized_access || forced != self.current_access || due_for_perm_check {
                self.last_permission_check = Some(now);
                self.apply_access_transition(state, forced);
            }
        } else if due_for_perm_check {
            let initial = !self.has_initialized_access;
            self.last_permission_check = Some(now);
            let queried = self.backend.query_access(initial);
            self.apply_access_transition(state, queried);
        }

        // 2. If not Allowed, do NOT poll notifications; sleep at low-CPU backoff until next permission check
        if !self.listener_running || self.current_access != NotifAccessState::Allowed {
            return self.permission_interval.as_millis().min(3000) as u64;
        }

        // 3. Poll notifications (empty list is NEVER treated as permission loss!)
        match self.backend.fetch_toasts() {
            Ok(toasts) => {
                self.consecutive_fetch_errors = 0;
                state.notif_polls.fetch_add(1, Ordering::Relaxed);
                state
                    .notif_history
                    .store(toasts.len() as u64, Ordering::Relaxed);

                let mut current_ids: HashSet<u32> = HashSet::with_capacity(toasts.len());
                let mut new_in_this_poll = 0u32;

                for t in toasts {
                    current_ids.insert(t.id);
                    if !self.active_meta.contains_key(&t.id) {
                        self.active_meta
                            .insert(t.id, (t.app_id.clone(), t.app_name.clone()));

                        if self.dedup.check_and_record(t.id, &t.app_id, now) {
                            state
                                .notif_duplicates_suppressed
                                .fetch_add(1, Ordering::Relaxed);
                        } else {
                            new_in_this_poll += 1;
                            state.notif_new.fetch_add(1, Ordering::Relaxed);
                            if state.effective_master_enabled() {
                                let aid_clone = t.app_id.clone();
                                let aname_clone = t.app_name.clone();
                                push_notification_event(
                                    state,
                                    "Added",
                                    t.id,
                                    t.app_id,
                                    t.app_name,
                                    t.timestamp_unix_ms,
                                );

                                // Phase 3D: Autonomous Rust Glow trigger when UseRustGlowEngine=true (works even when UI is closed)
                                if let Ok(cfg) = state.config.read() {
                                    if cfg.settings.UseRustGlowEngine {
                                        if let Some(profile) = crate::models::match_profile(
                                            &cfg.profiles,
                                            &aid_clone,
                                            &aname_clone,
                                        ) {
                                            if profile.Enabled {
                                                let mut eff_settings = cfg.settings.clone();
                                                eff_settings.MasterEnabled =
                                                    state.effective_master_enabled();
                                                eff_settings.GamingModeEnabled =
                                                    state.effective_gaming_mode();
                                                let active_game = state
                                                    .game_detector
                                                    .current_active_game();
                                                let is_gaming = eff_settings.GamingModeEnabled
                                                    && active_game.is_some();
                                                let active_id = active_game
                                                    .as_ref()
                                                    .map(|g| g.identifier.as_str());
                                                let _ = state.glow_engine.trigger(
                                                    profile,
                                                    &eff_settings,
                                                    is_gaming,
                                                    active_id,
                                                    false,
                                                    None,
                                                );
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Detect dismissed/removed notifications
                let removed_ids: Vec<u32> = self
                    .active_meta
                    .keys()
                    .copied()
                    .filter(|id| !current_ids.contains(id))
                    .collect();

                if !removed_ids.is_empty() {
                    state
                        .notif_removed
                        .fetch_add(removed_ids.len() as u64, Ordering::Relaxed);
                    let rm_ts = now_unix_ms();
                    for rid in removed_ids {
                        if let Some((aid, aname)) = self.active_meta.remove(&rid) {
                            push_notification_event(state, "Removed", rid, aid, aname, rm_ts);
                        }
                    }
                }

                if new_in_this_poll > 0 {
                    self.empty_polls = 0;
                } else if self.empty_polls < 50 {
                    self.empty_polls += 1;
                }
            }
            Err(hr) => {
                self.consecutive_fetch_errors += 1;
                (state.log_cb)(&format!(
                    "GetNotificationsAsync error (count={}, hr=0x{:08X}); scheduling immediate access re-check",
                    self.consecutive_fetch_errors, hr as u32
                ));
                // Force immediate permission re-check on next step
                self.last_permission_check = None;
                if self.consecutive_fetch_errors >= 3 {
                    // Tear down listener and mark Unknown until next access query confirms Allowed
                    self.apply_access_transition(state, NotifAccessState::Unknown);
                    return 1000;
                }
            }
        }

        if state.notif_event_mode.load(Ordering::Relaxed) {
            1000
        } else if self.empty_polls > 20 {
            400
        } else if self.empty_polls > 8 {
            300
        } else {
            200
        }
    }

    pub fn shutdown(&mut self, state: &Arc<WorkerState>) {
        if self.listener_running {
            self.backend.stop_listener_session();
            self.listener_running = false;
            self.active_meta.clear();
            state.notif_event_mode.store(false, Ordering::Relaxed);
            state.active_listener_count.store(0, Ordering::SeqCst);
        }
    }
}

struct WinRtNotificationBackend {
    listener: Option<UserNotificationListener>,
    event_token: Option<windows::Foundation::EventRegistrationToken>,
}

impl WinRtNotificationBackend {
    fn new() -> Self {
        Self {
            listener: None,
            event_token: None,
        }
    }

    fn ensure_listener(&mut self) -> Option<&UserNotificationListener> {
        if self.listener.is_none() {
            if let Ok(l) = UserNotificationListener::Current() {
                self.listener = Some(l);
            }
        }
        self.listener.as_ref()
    }
}

impl NotificationBackend for WinRtNotificationBackend {
    fn query_access(&mut self, initial_request: bool) -> NotifAccessState {
        let Some(listener) = self.ensure_listener() else {
            return NotifAccessState::Unknown;
        };

        // On initial check use RequestAccessAsync(); on periodic checks use non-prompting GetAccessStatus()
        let res = if initial_request {
            listener.RequestAccessAsync().and_then(|op| op.get())
        } else {
            listener.GetAccessStatus()
        };

        match res {
            Ok(status) => NotifAccessState::from_winrt(status),
            Err(_) => NotifAccessState::Unknown,
        }
    }

    fn start_listener_session(&mut self, state: &Arc<WorkerState>) -> bool {
        self.stop_listener_session();
        let Some(listener) = self.ensure_listener() else {
            return false;
        };

        // Enable event-driven mode ONLY when MSIX package identity is verified AND subscription succeeds
        if has_msix_package_identity() {
            let wake_state = Arc::clone(state);
            let handler =
                TypedEventHandler::<UserNotificationListener, UserNotificationChangedEventArgs>::new(
                    move |_sender, _args| {
                        wake_state.notif_event_signaled.store(true, Ordering::Release);
                        Ok(())
                    },
                );
            if let Ok(token) = listener.NotificationChanged(&handler) {
                self.event_token = Some(token);
                return true;
            }
        }
        false
    }

    fn stop_listener_session(&mut self) {
        if let (Some(listener), Some(token)) = (self.listener.as_ref(), self.event_token.take()) {
            let _ = listener.RemoveNotificationChanged(token);
        }
    }

    fn fetch_toasts(&mut self) -> Result<Vec<PolledToast>, i32> {
        let Some(listener) = self.ensure_listener() else {
            return Err(-1);
        };
        let list = listener
            .GetNotificationsAsync(NotificationKinds::Toast)
            .and_then(|op| op.get())
            .map_err(|e| e.code().0)?;
        let sz = list.Size().map_err(|e| e.code().0)?;
        let mut out = Vec::with_capacity(sz as usize);
        for i in 0..sz {
            if let Ok(n) = list.GetAt(i) {
                if let Ok(id) = n.Id() {
                    let (app_id, app_name, timestamp_unix_ms) = extract_safe_app_identity(&n);
                    out.push(PolledToast {
                        id,
                        app_id,
                        app_name,
                        timestamp_unix_ms,
                    });
                }
            }
        }
        Ok(out)
    }
}

pub fn run_notification_service(state: Arc<WorkerState>, simulate_denied: bool) {
    unsafe {
        let _ = CoInitializeEx(None, COINIT_MULTITHREADED);
    }

    if simulate_denied {
        state
            .test_access_override
            .store(NotifAccessState::Denied as u64, Ordering::SeqCst);
    }

    let backend = WinRtNotificationBackend::new();
    let perm_interval = if simulate_denied {
        Duration::from_millis(1000)
    } else {
        Duration::from_millis(PERMISSION_RECHECK_INTERVAL_MS)
    };
    let mut core = NotificationMonitorCore::new(backend, perm_interval);

    while state.running.load(Ordering::Relaxed) {
        state.notif_event_signaled.store(false, Ordering::Relaxed);
        let wait_ms = core.step(&state, Instant::now());

        let mut slept = 0u64;
        while slept < wait_ms && state.running.load(Ordering::Relaxed) {
            if state.notif_event_signaled.load(Ordering::Acquire) {
                break;
            }
            let step = 100u64.min(wait_ms - slept);
            thread::sleep(Duration::from_millis(step));
            slept += step;
        }
    }

    core.shutdown(&state);

    unsafe {
        CoUninitialize();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::glow::GlowEngine;
    use crate::ipc::ConfigSnapshot;
    use std::sync::atomic::{AtomicBool, AtomicU64};
    use std::sync::{Mutex, RwLock};

    struct MockBackend {
        access: NotifAccessState,
        toasts: Result<Vec<PolledToast>, i32>,
        starts: u32,
        stops: u32,
        active_sessions: i32,
        max_concurrent_sessions: i32,
        access_queries: u32,
    }

    impl MockBackend {
        fn new(access: NotifAccessState) -> Self {
            Self {
                access,
                toasts: Ok(Vec::new()),
                starts: 0,
                stops: 0,
                active_sessions: 0,
                max_concurrent_sessions: 0,
                access_queries: 0,
            }
        }
    }

    impl NotificationBackend for MockBackend {
        fn query_access(&mut self, _initial: bool) -> NotifAccessState {
            self.access_queries += 1;
            self.access
        }
        fn start_listener_session(&mut self, _state: &Arc<WorkerState>) -> bool {
            self.starts += 1;
            self.active_sessions += 1;
            if self.active_sessions > self.max_concurrent_sessions {
                self.max_concurrent_sessions = self.active_sessions;
            }
            false
        }
        fn stop_listener_session(&mut self) {
            self.stops += 1;
            self.active_sessions -= 1;
        }
        fn fetch_toasts(&mut self) -> Result<Vec<PolledToast>, i32> {
            self.toasts.clone()
        }
    }

    fn make_unit_state() -> Arc<WorkerState> {
        static RUN: AtomicBool = AtomicBool::new(true);
        static TRAY: AtomicBool = AtomicBool::new(false);
        static REST: AtomicU64 = AtomicU64::new(0);
        static ACC: AtomicU64 = AtomicU64::new(0);
        static POL: AtomicU64 = AtomicU64::new(0);
        static NEW: AtomicU64 = AtomicU64::new(0);
        static DEN: AtomicU64 = AtomicU64::new(0);
        Arc::new(WorkerState {
            start_time: Instant::now(),
            running: &RUN,
            shutting_down: AtomicBool::new(false),
            ipc_only: false,
            config_dir: None,
            ui_exe_path: None,
            reload_lock: Mutex::new(()),
            config: RwLock::new(ConfigSnapshot::default()),
            glow_engine: GlowEngine::new(&RUN),
            game_detector: crate::games::GameDetectorState::new(),
            tray_active: &TRAY,
            taskbar_restores: &REST,
            notif_access: &ACC,
            notif_polls: &POL,
            notif_new: &NEW,
            notif_denied_checks: &DEN,
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
    fn test_deduplicator_snapshot_and_burst() {
        let mut d = NotificationDeduplicator::new();
        d.seed_snapshot_id(100);
        let t0 = Instant::now();
        assert!(d.check_and_record(100, "discord.exe", t0));
        assert!(!d.check_and_record(101, "discord.exe", t0));
        assert!(d.check_and_record(101, "discord.exe", t0));
        assert!(d.check_and_record(102, "Discord.EXE", t0 + Duration::from_millis(50)));
        assert!(!d.check_and_record(103, "discord.exe", t0 + Duration::from_millis(400)));
    }

    #[test]
    fn test_allowed_to_denied_to_allowed_transitions_and_single_listener() {
        let st = make_unit_state();
        let backend = MockBackend::new(NotifAccessState::Allowed);
        let mut core = NotificationMonitorCore::new(backend, Duration::from_secs(3));
        let t0 = Instant::now();

        // 1. Initial Allowed -> starts exactly 1 listener
        core.step(&st, t0);
        assert_eq!(core.current_access(), NotifAccessState::Allowed);
        assert!(core.is_listener_running());
        assert_eq!(st.active_listener_count.load(Ordering::SeqCst), 1);
        assert_eq!(core.backend.starts, 1);

        // 2. Empty list while Allowed -> MUST NOT be treated as permission loss!
        core.step(&st, t0 + Duration::from_secs(4));
        assert_eq!(core.current_access(), NotifAccessState::Allowed);
        assert!(core.is_listener_running());
        assert_eq!(core.backend.starts, 1, "No duplicate listener started on re-check");

        // 3. Allowed -> Denied (even though fetch_toasts still returns Ok(empty))
        core.backend.access = NotifAccessState::Denied;
        core.step(&st, t0 + Duration::from_secs(8));
        assert_eq!(core.current_access(), NotifAccessState::Denied);
        assert!(!core.is_listener_running());
        assert_eq!(st.active_listener_count.load(Ordering::SeqCst), 0);
        assert_eq!(core.backend.stops, 1);

        // 4. Denied -> Allowed -> restarts single listener cleanly & detects new toast
        core.backend.access = NotifAccessState::Allowed;
        core.step(&st, t0 + Duration::from_secs(12));
        assert_eq!(core.current_access(), NotifAccessState::Allowed);
        assert!(core.is_listener_running());
        assert_eq!(st.active_listener_count.load(Ordering::SeqCst), 1);
        assert_eq!(core.backend.starts, 2);
        assert_eq!(core.backend.max_concurrent_sessions, 1, "Never >1 active listener!");

        // Verify AccessChanged events were emitted to IPC queue
        let events = st.notif_events.lock().unwrap();
        let access_events: Vec<&NotificationEventRecord> =
            events.iter().filter(|e| e.kind == "AccessChanged").collect();
        assert!(access_events.len() >= 3);
        assert_eq!(access_events[0].app_id, "Allowed");
        assert_eq!(access_events[1].app_id, "Denied");
        assert_eq!(access_events[2].app_id, "Allowed");
    }

    #[test]
    fn test_get_notifications_error_triggers_recheck_and_unknown_never_assumed_allowed() {
        let st = make_unit_state();
        let mut backend = MockBackend::new(NotifAccessState::Unknown);
        backend.toasts = Err(-2147024891); // E_ACCESSDENIED
        let mut core = NotificationMonitorCore::new(backend, Duration::from_secs(3));
        let t0 = Instant::now();

        // Unknown access -> never assumed Allowed, listener NOT started
        core.step(&st, t0);
        assert_eq!(core.current_access(), NotifAccessState::Unknown);
        assert!(!core.is_listener_running());
        assert_eq!(st.active_listener_count.load(Ordering::SeqCst), 0);

        // Switch to Allowed, then fail GetNotificationsAsync 3 times -> falls back to Unknown
        core.backend.access = NotifAccessState::Allowed;
        core.backend.toasts = Ok(Vec::new());
        core.step(&st, t0 + Duration::from_secs(4));
        assert!(core.is_listener_running());

        core.backend.toasts = Err(-2147024891);
        core.step(&st, t0 + Duration::from_secs(5));
        core.step(&st, t0 + Duration::from_secs(6));
        core.step(&st, t0 + Duration::from_secs(7));
        assert_eq!(core.current_access(), NotifAccessState::Unknown);
        assert!(!core.is_listener_running());
        assert_eq!(st.active_listener_count.load(Ordering::SeqCst), 0);
    }
}
