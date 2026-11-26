#![windows_subsystem = "windows"]
#![recursion_limit = "256"]

mod games;
mod glow;
mod ipc;
mod models;
mod notifications;

use std::collections::VecDeque;
use std::env;
use std::fs::OpenOptions;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, AtomicIsize, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, OnceLock, RwLock};
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use windows::core::PCWSTR;
use windows::Win32::Foundation::{
    CloseHandle, GetLastError, ERROR_ALREADY_EXISTS, HINSTANCE, HWND, LPARAM, LRESULT, POINT,
    WPARAM,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Threading::{CreateMutexW, ReleaseMutex};
use windows::Win32::UI::HiDpi::{
    GetDpiForWindow, GetSystemMetricsForDpi, SetProcessDpiAwarenessContext,
    DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
};
use windows::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_MESSAGE, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_SETVERSION,
    NOTIFYICONDATAW, NOTIFYICON_VERSION_4,
};
use windows::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CreatePopupMenu, CreateWindowExW, DefWindowProcW, DestroyIcon, DestroyMenu,
    DestroyWindow, DispatchMessageW, GetCursorPos, GetMessageW, LoadIconW, LoadImageW,
    PostMessageW, PostQuitMessage, RegisterClassExW, RegisterWindowMessageW, SetForegroundWindow,
    TrackPopupMenuEx, TranslateMessage, UnregisterClassW, CS_DBLCLKS, HICON, IDI_APPLICATION,
    IMAGE_ICON, LR_LOADFROMFILE, MF_CHECKED, MF_SEPARATOR, MF_STRING, MSG, SM_CXSMICON,
    SM_CYSMICON, TPM_BOTTOMALIGN, TPM_RIGHTBUTTON, WINDOW_EX_STYLE, WINDOW_STYLE, WM_APP,
    WM_CLOSE, WM_COMMAND, WM_CONTEXTMENU, WM_DESTROY, WM_DISPLAYCHANGE, WM_DPICHANGED,
    WM_ENDSESSION, WM_LBUTTONDBLCLK, WM_LBUTTONUP, WM_QUERYENDSESSION, WM_RBUTTONUP, WNDCLASSEXW,
};

use games::{run_game_monitor_service, GameDetectorState};
use glow::GlowEngine;
use ipc::{default_pipe_name, run_ipc_server, ConfigSnapshot, WorkerState};
use notifications::run_notification_service;

const WM_TRAYICON: u32 = WM_APP + 1;
const IDM_SHOW: usize = 1001;
const IDM_TOGGLE_NOTIF: usize = 1002;
const IDM_TOGGLE_GAMING: usize = 1003;
const IDM_TEST_GLOW: usize = 1004;
const IDM_UPDATES: usize = 1005;
const IDM_EXIT: usize = 1006;

static mut G_WM_TASKBAR_CREATED: u32 = 0;
static G_HWND_RAW: AtomicIsize = AtomicIsize::new(0);
static mut G_HICON: HICON = HICON(std::ptr::null_mut());
static mut G_OWNED_HICON: bool = false;
static mut G_NID: NOTIFYICONDATAW = unsafe { std::mem::zeroed() };
static G_TRAY_ENABLED: AtomicBool = AtomicBool::new(false);
static G_TRAY_ACTIVE: AtomicBool = AtomicBool::new(false);
static G_TRAY_DELETED: AtomicBool = AtomicBool::new(true);
static G_TASKBAR_RESTORES: AtomicU64 = AtomicU64::new(0);

static G_RUNNING: AtomicBool = AtomicBool::new(true);
static G_SHUTDOWN_REQUESTED: AtomicBool = AtomicBool::new(false);
static G_NOTIF_ACCESS: AtomicU64 = AtomicU64::new(0);
static G_NOTIF_POLLS: AtomicU64 = AtomicU64::new(0);
static G_NOTIF_NEW: AtomicU64 = AtomicU64::new(0);
static G_NOTIF_DENIED_CHECKS: AtomicU64 = AtomicU64::new(0);
static G_LOG_PATH: OnceLock<String> = OnceLock::new();
static G_STATE: OnceLock<Arc<WorkerState>> = OnceLock::new();

struct CliOptions {
    ipc_only: bool,
    simulate_notif_denied: bool,
    pipe_name: String,
    mutex_name: String,
    config_dir: Option<String>,
    ui_exe_path: Option<String>,
}

fn parse_cli() -> CliOptions {
    let args: Vec<String> = env::args().collect();
    let mut opts = CliOptions {
        ipc_only: false,
        simulate_notif_denied: false,
        pipe_name: default_pipe_name(),
        mutex_name: "Local\\NotiGlow_Background_SingleInstance_v1".to_string(),
        config_dir: env::var("NOTIGLOW_CONFIG_DIR").ok().filter(|s| !s.trim().is_empty()),
        ui_exe_path: None,
    };
    let mut i = 1;
    while i < args.len() {
        match args[i].as_str() {
            "--ipc-only" => opts.ipc_only = true,
            "--simulate-notif-denied" => opts.simulate_notif_denied = true,
            "--pipe-name" if i + 1 < args.len() => {
                let raw = &args[i + 1];
                opts.pipe_name = if raw.starts_with(r"\\.\pipe\") {
                    raw.clone()
                } else {
                    format!(r"\\.\pipe\{}", raw)
                };
                i += 1;
            }
            "--mutex-name" if i + 1 < args.len() => {
                opts.mutex_name = args[i + 1].clone();
                i += 1;
            }
            "--config-dir" if i + 1 < args.len() => {
                opts.config_dir = Some(args[i + 1].clone());
                i += 1;
            }
            "--ui-exe" if i + 1 < args.len() => {
                opts.ui_exe_path = Some(args[i + 1].clone());
                i += 1;
            }
            "--log-file" if i + 1 < args.len() => {
                let _ = G_LOG_PATH.set(args[i + 1].clone());
                i += 1;
            }
            _ => {}
        }
        i += 1;
    }

    // Auto-discover sibling NotiGlow.exe if --ui-exe not explicitly passed
    if opts.ui_exe_path.is_none() {
        if let Ok(cur) = env::current_exe() {
            if let Some(dir) = cur.parent() {
                let sibling = dir.join("NotiGlow.exe");
                if sibling.exists() {
                    opts.ui_exe_path = Some(sibling.to_string_lossy().to_string());
                }
            }
        }
    }
    opts
}

fn log_event(msg: &str) {
    if let Some(path) = G_LOG_PATH.get() {
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_secs())
            .unwrap_or(0);
        if let Ok(mut f) = OpenOptions::new().create(true).append(true).open(path) {
            let _ = writeln!(f, "[{}] {}", ts, msg);
        }
    }
}

fn to_wide_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn get_main_hwnd() -> HWND {
    HWND(G_HWND_RAW.load(Ordering::SeqCst) as *mut std::ffi::c_void)
}

fn post_simulate_taskbar_created() {
    unsafe {
        let hwnd = get_main_hwnd();
        if !hwnd.0.is_null() && G_WM_TASKBAR_CREATED != 0 {
            let _ = PostMessageW(hwnd, G_WM_TASKBAR_CREATED, WPARAM(0), LPARAM(0));
        }
    }
}

fn request_clean_shutdown(source: &str) {
    G_RUNNING.store(false, Ordering::SeqCst);
    if let Some(st) = G_STATE.get() {
        st.shutting_down.store(true, Ordering::SeqCst);
        st.glow_engine.stop_all();
    }
    if !G_SHUTDOWN_REQUESTED.swap(true, Ordering::SeqCst) {
        log_event(&format!("Clean shutdown requested by {}", source));
        let hwnd = get_main_hwnd();
        if !hwnd.0.is_null() {
            unsafe {
                let _ = PostMessageW(hwnd, WM_CLOSE, WPARAM(0), LPARAM(0));
            }
        }
    }
}

unsafe fn resolve_tray_icon(cx: i32, cy: i32, ui_exe_path: Option<&str>) {
    let mut candidates: Vec<PathBuf> = Vec::new();
    if let Some(ui) = ui_exe_path {
        if let Some(d) = Path::new(ui).parent() {
            candidates.push(d.join("Assets").join("NotiGlow.ico"));
        }
    }
    if let Ok(cur) = env::current_exe() {
        let mut anc = cur.parent();
        for _ in 0..6 {
            if let Some(d) = anc {
                candidates.push(d.join("Assets").join("NotiGlow.ico"));
                anc = d.parent();
            }
        }
    }

    for ico in candidates {
        if ico.exists() {
            let ico_w = to_wide_null(&ico.to_string_lossy());
            if let Ok(h) = LoadImageW(
                HINSTANCE(std::ptr::null_mut()),
                PCWSTR(ico_w.as_ptr()),
                IMAGE_ICON,
                cx,
                cy,
                LR_LOADFROMFILE,
            ) {
                if !h.0.is_null() {
                    G_HICON = HICON(h.0);
                    G_OWNED_HICON = true;
                    return;
                }
            }
        }
    }

    // Guaranteed system application icon fallback so Shell_NotifyIconW never fails with null HICON
    if let Ok(sys_ico) = LoadIconW(HINSTANCE(std::ptr::null_mut()), IDI_APPLICATION) {
        G_HICON = sys_ico;
        G_OWNED_HICON = false;
    }
}

unsafe fn add_tray_icon() {
    if !G_TRAY_ENABLED.load(Ordering::SeqCst) {
        return;
    }
    let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
    nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
    nid.hWnd = get_main_hwnd();
    nid.uID = 1;
    nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    nid.uCallbackMessage = WM_TRAYICON;
    nid.hIcon = G_HICON;
    let tip = to_wide_null("NotiGlow");
    for (i, &c) in tip.iter().take(127).enumerate() {
        nid.szTip[i] = c;
    }
    let added = Shell_NotifyIconW(NIM_ADD, &nid).as_bool();
    nid.Anonymous.uVersion = NOTIFYICON_VERSION_4;
    let _ = Shell_NotifyIconW(NIM_SETVERSION, &nid);
    G_NID = nid;
    G_TRAY_ACTIVE.store(added, Ordering::SeqCst);
    G_TRAY_DELETED.store(false, Ordering::SeqCst);
}

unsafe extern "system" fn wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    if msg == G_WM_TASKBAR_CREATED && G_WM_TASKBAR_CREATED != 0 {
        add_tray_icon();
        G_TASKBAR_RESTORES.fetch_add(1, Ordering::Relaxed);
        return LRESULT(0);
    }
    if msg == WM_TRAYICON && G_TRAY_ENABLED.load(Ordering::Relaxed) {
        let ev = (lparam.0 & 0xFFFF) as u32;
        if ev == WM_RBUTTONUP || ev == WM_CONTEXTMENU {
            let (master_on, gaming_on) = G_STATE
                .get()
                .map(|s| (s.effective_master_enabled(), s.effective_gaming_mode()))
                .unwrap_or((true, false));

            if let Ok(hmenu) = CreatePopupMenu() {
                let notif_flags = if master_on {
                    MF_STRING | MF_CHECKED
                } else {
                    MF_STRING
                };
                let gaming_flags = if gaming_on {
                    MF_STRING | MF_CHECKED
                } else {
                    MF_STRING
                };
                let items = [
                    (MF_STRING, IDM_SHOW, "Open NotiGlow"),
                    (MF_SEPARATOR, 0, ""),
                    (notif_flags, IDM_TOGGLE_NOTIF, "Notifications Enabled"),
                    (gaming_flags, IDM_TOGGLE_GAMING, "Gaming Mode"),
                    (MF_SEPARATOR, 0, ""),
                    (MF_STRING, IDM_TEST_GLOW, "Test Glow Animation"),
                    (MF_STRING, IDM_UPDATES, "Check for Updates"),
                    (MF_SEPARATOR, 0, ""),
                    (MF_STRING, IDM_EXIT, "Exit"),
                ];
                for (flags, id, label) in items {
                    if label.is_empty() {
                        let _ = AppendMenuW(hmenu, flags, id, PCWSTR::null());
                    } else {
                        let w = to_wide_null(label);
                        let _ = AppendMenuW(hmenu, flags, id, PCWSTR(w.as_ptr()));
                    }
                }
                let mut pt = POINT::default();
                let _ = GetCursorPos(&mut pt);
                let _ = SetForegroundWindow(hwnd);
                let _ = TrackPopupMenuEx(
                    hmenu,
                    (TPM_RIGHTBUTTON | TPM_BOTTOMALIGN).0,
                    pt.x,
                    pt.y,
                    hwnd,
                    None,
                );
                let _ = DestroyMenu(hmenu);
            }
            return LRESULT(0);
        } else if ev == WM_LBUTTONUP || ev == WM_LBUTTONDBLCLK {
            if let Some(st) = G_STATE.get() {
                let _ = st.open_or_signal_ui(Some("General"), true);
            }
            return LRESULT(0);
        }
    }
    if msg == WM_COMMAND {
        let cmd_id = wparam.0 & 0xFFFF;
        if let Some(st) = G_STATE.get() {
            match cmd_id {
                IDM_SHOW => {
                    let _ = st.open_or_signal_ui(Some("General"), false);
                    return LRESULT(0);
                }
                IDM_TOGGLE_NOTIF => {
                    let next = !st.effective_master_enabled();
                    st.set_master_override(next, true);
                    return LRESULT(0);
                }
                IDM_TOGGLE_GAMING => {
                    let next = !st.effective_gaming_mode();
                    st.set_gaming_override(next, true);
                    return LRESULT(0);
                }
                IDM_TEST_GLOW => {
                    let use_rust_glow = st
                        .config
                        .read()
                        .map(|c| c.settings.UseRustGlowEngine)
                        .unwrap_or(false);
                    if use_rust_glow {
                        let settings = st
                            .config
                            .read()
                            .map(|c| c.settings.clone())
                            .unwrap_or_default();
                        let test_profile = models::AppProfile {
                            AppId: "TestProfile".to_string(),
                            Name: "NotiGlow Test".to_string(),
                            ColorHex: settings.DefaultColorHex.clone(),
                            DurationMs: settings.DefaultDurationMs,
                            Intensity: settings.DefaultIntensity,
                            Thickness: settings.DefaultThickness,
                            GlowSize: settings.DefaultGlowSize,
                            CornerRadius: settings.CornerRadius,
                            Style: settings.DefaultStyle,
                            ..Default::default()
                        };
                        let _ = st.glow_engine.trigger(&test_profile, &settings, false, None, true, None);
                    } else {
                        st.push_tray_action("TestAnimation", None);
                        let _ = st.open_or_signal_ui(None, false);
                    }
                    return LRESULT(0);
                }
                IDM_UPDATES => {
                    let _ = st.open_or_signal_ui(Some("Advanced"), false);
                    return LRESULT(0);
                }
                IDM_EXIT => {
                    st.push_tray_action("Exit", None);
                    request_clean_shutdown("TRAY_MENU_EXIT");
                    return LRESULT(0);
                }
                _ => {}
            }
        }
    }
    if msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED {
        if let Some(st) = G_STATE.get() {
            let _ = st.glow_engine.notify_monitor_changed();
        }
        return LRESULT(0);
    }
    if msg == WM_QUERYENDSESSION {
        return LRESULT(1);
    }
    if msg == WM_ENDSESSION {
        if wparam.0 != 0 {
            request_clean_shutdown("WM_ENDSESSION");
        }
        return LRESULT(0);
    }
    if msg == WM_CLOSE {
        G_RUNNING.store(false, Ordering::SeqCst);
        if let Some(st) = G_STATE.get() {
            st.glow_engine.stop_all();
        }
        let _ = DestroyWindow(hwnd);
        return LRESULT(0);
    }
    if msg == WM_DESTROY {
        G_RUNNING.store(false, Ordering::SeqCst);
        if G_TRAY_ENABLED.load(Ordering::SeqCst) && !G_TRAY_DELETED.swap(true, Ordering::SeqCst) {
            let _ = Shell_NotifyIconW(NIM_DELETE, &raw const G_NID);
            G_TRAY_ACTIVE.store(false, Ordering::SeqCst);
        }
        PostQuitMessage(0);
        return LRESULT(0);
    }
    DefWindowProcW(hwnd, msg, wparam, lparam)
}

fn main() {
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }

    let opts = parse_cli();

    let mutex_wide = to_wide_null(&opts.mutex_name);
    let h_mutex = unsafe { CreateMutexW(None, true, PCWSTR(mutex_wide.as_ptr())) };
    if let Ok(hm) = h_mutex {
        if unsafe { GetLastError() } == ERROR_ALREADY_EXISTS {
            unsafe {
                let _ = CloseHandle(hm);
            }
            std::process::exit(2);
        }
    }

    let glow_engine = GlowEngine::new(&G_RUNNING);
    let game_detector = GameDetectorState::new();
    let state = Arc::new(WorkerState {
        start_time: Instant::now(),
        running: &G_RUNNING,
        shutting_down: AtomicBool::new(false),
        ipc_only: opts.ipc_only,
        config_dir: opts.config_dir.clone(),
        ui_exe_path: opts.ui_exe_path.clone(),
        reload_lock: Mutex::new(()),
        config: RwLock::new(ConfigSnapshot::default()),
        glow_engine,
        game_detector,
        tray_active: &G_TRAY_ACTIVE,
        taskbar_restores: &G_TASKBAR_RESTORES,
        notif_access: &G_NOTIF_ACCESS,
        notif_polls: &G_NOTIF_POLLS,
        notif_new: &G_NOTIF_NEW,
        notif_denied_checks: &G_NOTIF_DENIED_CHECKS,
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
        simulate_taskbar_cb: post_simulate_taskbar_created,
        request_shutdown_cb: request_clean_shutdown,
        log_cb: log_event,
    });
    let _ = state.reload_config_from_disk(None, false);
    let _ = G_STATE.set(Arc::clone(&state));

    log_event(&format!(
        "NotiGlow.Background started (ipc_only={}, pipe={})",
        opts.ipc_only, opts.pipe_name
    ));

    if !opts.ipc_only {
        G_TRAY_ENABLED.store(true, Ordering::SeqCst);
        let notif_state = Arc::clone(&state);
        let sim_denied = opts.simulate_notif_denied;
        thread::spawn(move || run_notification_service(notif_state, sim_denied));

        let game_state = Arc::clone(&state);
        thread::spawn(move || run_game_monitor_service(game_state));
    }

    let pipe_name = opts.pipe_name.clone();
    let ipc_state = Arc::clone(&state);
    thread::spawn(move || run_ipc_server(pipe_name, ipc_state));

    unsafe {
        let hinst: HINSTANCE = GetModuleHandleW(PCWSTR::null()).unwrap().into();
        let class_name = to_wide_null("NotiGlow_Background_MsgWindow_v1");
        let win_title = to_wide_null("NotiGlow Background Worker");

        let wc = WNDCLASSEXW {
            cbSize: std::mem::size_of::<WNDCLASSEXW>() as u32,
            style: CS_DBLCLKS,
            lpfnWndProc: Some(wnd_proc),
            hInstance: hinst,
            lpszClassName: PCWSTR(class_name.as_ptr()),
            ..Default::default()
        };
        RegisterClassExW(&wc);

        let hwnd = CreateWindowExW(
            WINDOW_EX_STYLE(0),
            PCWSTR(class_name.as_ptr()),
            PCWSTR(win_title.as_ptr()),
            WINDOW_STYLE(0),
            0,
            0,
            0,
            0,
            HWND(std::ptr::null_mut()),
            None,
            hinst,
            None,
        )
        .unwrap_or(HWND(std::ptr::null_mut()));
        G_HWND_RAW.store(hwnd.0 as isize, Ordering::SeqCst);

        let taskbar_msg = to_wide_null("TaskbarCreated");
        G_WM_TASKBAR_CREATED = RegisterWindowMessageW(PCWSTR(taskbar_msg.as_ptr()));

        if G_TRAY_ENABLED.load(Ordering::SeqCst) {
            let mut dpi = GetDpiForWindow(hwnd);
            if dpi == 0 {
                dpi = 96;
            }
            let cx = GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            let cy = GetSystemMetricsForDpi(SM_CYSMICON, dpi);
            resolve_tray_icon(cx, cy, opts.ui_exe_path.as_deref());
            add_tray_icon();
        }

        if !G_RUNNING.load(Ordering::SeqCst) && !hwnd.0.is_null() {
            let _ = PostMessageW(hwnd, WM_CLOSE, WPARAM(0), LPARAM(0));
        }

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, HWND(std::ptr::null_mut()), 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        state.glow_engine.stop_all();
        let wait_start = Instant::now();
        while state.glow_engine.active_window_count() > 0
            && wait_start.elapsed() < Duration::from_millis(500)
        {
            thread::sleep(Duration::from_millis(10));
        }

        if G_TRAY_ENABLED.load(Ordering::SeqCst) && !G_TRAY_DELETED.swap(true, Ordering::SeqCst) {
            let _ = Shell_NotifyIconW(NIM_DELETE, &raw const G_NID);
            G_TRAY_ACTIVE.store(false, Ordering::SeqCst);
        }
        if G_OWNED_HICON && !G_HICON.0.is_null() {
            let _ = DestroyIcon(G_HICON);
        }
        G_HICON = HICON(std::ptr::null_mut());
        let _ = UnregisterClassW(PCWSTR(class_name.as_ptr()), hinst);
        G_HWND_RAW.store(0, Ordering::SeqCst);

        if let Ok(hm) = h_mutex {
            let _ = ReleaseMutex(hm);
            let _ = CloseHandle(hm);
        }
        log_event("NotiGlow.Background exited cleanly");
    }
}
