use std::collections::VecDeque;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};

use windows::core::PCWSTR;
use windows::Win32::Foundation::{BOOL, COLORREF, HINSTANCE, HWND, LPARAM, LRESULT, POINT, RECT, SIZE, WPARAM};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, CreateDIBSection, DeleteDC, DeleteObject, EnumDisplayMonitors,
    GetMonitorInfoW, SelectObject, AC_SRC_ALPHA, AC_SRC_OVER, BITMAPINFO, BITMAPINFOHEADER,
    BI_RGB, BLENDFUNCTION, DIB_RGB_COLORS, HDC, HMONITOR, MONITORINFOEXW,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::HiDpi::{GetDpiForMonitor, MDT_EFFECTIVE_DPI};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, GetCursorPos, RegisterClassExW, ShowWindow,
    UpdateLayeredWindow, SW_SHOWNOACTIVATE, ULW_ALPHA, WNDCLASSEXW, WS_EX_LAYERED,
    WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TOPMOST, WS_EX_TRANSPARENT, WS_POPUP,
};

use crate::models::{
    evaluate_and_adjust_profile, is_rgb_spectrum, parse_color_rgb, AppProfile, AppSettings,
    BurstMode, GlowStyle, MonitorMode, TriggerDecision, RGB_SPECTRUM_COLORS,
};

#[derive(Debug, Clone)]
pub struct MonitorRect {
    pub left: i32,
    pub top: i32,
    pub width: i32,
    pub height: i32,
    pub dpi: u32,
    pub is_primary: bool,
    pub device_name: String,
}

impl MonitorRect {
    pub fn contains(&self, x: i32, y: i32) -> bool {
        x >= self.left && x < (self.left + self.width) && y >= self.top && y < (self.top + self.height)
    }

    pub fn dpi_scale(&self) -> f64 {
        (self.dpi.max(96) as f64) / 96.0
    }
}

unsafe extern "system" fn enum_monitor_cb(
    hmon: HMONITOR,
    _hdc: HDC,
    _rect: *mut RECT,
    lparam: LPARAM,
) -> BOOL {
    let list = &mut *(lparam.0 as *mut Vec<MonitorRect>);
    let mut info: MONITORINFOEXW = std::mem::zeroed();
    info.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
    if GetMonitorInfoW(hmon, &mut info.monitorInfo as *mut _ as *mut _).as_bool() {
        let rc = info.monitorInfo.rcMonitor;
        let mut dpi_x = 96u32;
        let mut dpi_y = 96u32;
        let _ = GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, &mut dpi_x, &mut dpi_y);
        let end = info
            .szDevice
            .iter()
            .position(|&c| c == 0)
            .unwrap_or(info.szDevice.len());
        let dev = String::from_utf16_lossy(&info.szDevice[..end]);
        list.push(MonitorRect {
            left: rc.left,
            top: rc.top,
            width: (rc.right - rc.left).max(320),
            height: (rc.bottom - rc.top).max(240),
            dpi: if dpi_x == 0 { 96 } else { dpi_x },
            is_primary: (info.monitorInfo.dwFlags & 1) != 0,
            device_name: dev,
        });
    }
    BOOL(1)
}

pub fn enumerate_monitors() -> Vec<MonitorRect> {
    let mut monitors: Vec<MonitorRect> = Vec::new();
    unsafe {
        let _ = EnumDisplayMonitors(
            HDC(std::ptr::null_mut()),
            None,
            Some(enum_monitor_cb),
            LPARAM(&mut monitors as *mut Vec<MonitorRect> as isize),
        );
    }
    if monitors.is_empty() {
        monitors.push(MonitorRect {
            left: 0,
            top: 0,
            width: 1920,
            height: 1080,
            dpi: 96,
            is_primary: true,
            device_name: "FALLBACK".to_string(),
        });
    }
    monitors
}

pub fn select_target_monitors(
    mode: MonitorMode,
    cursor_x: i32,
    cursor_y: i32,
    monitors: &[MonitorRect],
) -> Vec<MonitorRect> {
    if monitors.is_empty() {
        return Vec::new();
    }
    match mode {
        MonitorMode::AllMonitors => monitors.to_vec(),
        MonitorMode::PrimaryMonitor => {
            if let Some(p) = monitors.iter().find(|m| m.is_primary) {
                vec![p.clone()]
            } else {
                vec![monitors[0].clone()]
            }
        }
        MonitorMode::ActiveMonitor => {
            if let Some(m) = monitors.iter().find(|m| m.contains(cursor_x, cursor_y)) {
                vec![m.clone()]
            } else if let Some(p) = monitors.iter().find(|m| m.is_primary) {
                vec![p.clone()]
            } else {
                vec![monitors[0].clone()]
            }
        }
    }
}

#[derive(Debug, Clone)]
pub struct GlowRequest {
    pub profile: AppProfile,
    pub reduce_motion_static: bool,
    pub monitor_mode: MonitorMode,
    pub dump_bmp_path: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GlowEventRecord {
    pub seq: u64,
    pub kind: String, // "GLOW_STARTED" | "GLOW_STOPPED" | "MONITOR_CHANGED"
    pub style: String,
    pub color_hex: String,
    pub duration_ms: i32,
    pub intensity: f64,
    pub monitors: usize,
    pub timestamp_unix_ms: i64,
}

#[derive(Debug, Clone)]
pub struct GlowTriggerOutcome {
    pub action: &'static str,
    pub style: String,
    pub duration_ms: i32,
    pub intensity: f64,
    pub corner_radius: f64,
    pub monitors: usize,
}

pub struct GlowEngine {
    global_running: &'static AtomicBool,
    generation: AtomicU64,
    is_animating: AtomicBool,
    active_windows: AtomicU64,
    extend_total_ms: AtomicU64,
    queue: Mutex<VecDeque<GlowRequest>>,
    next_event_seq: AtomicU64,
    events: Mutex<VecDeque<GlowEventRecord>>,
    pub total_triggers: AtomicU64,
    pub completed_count: AtomicU64,
    pub dropped_count: AtomicU64,
    pub queued_count: AtomicU64,
    pub extended_count: AtomicU64,
    pub restarted_count: AtomicU64,
    pub last_start_latency_us: AtomicU64,
    pub last_duration_ms: AtomicU64,
    pub last_frame_count: AtomicU64,
    pub last_avg_frame_us: AtomicU64,
    pub windows_created: AtomicU64,
    pub windows_destroyed: AtomicU64,
    pub dibs_created: AtomicU64,
    pub dibs_destroyed: AtomicU64,
    pub monitor_changes: AtomicU64,
    pub monitors_detected: AtomicU64,
}

impl GlowEngine {
    pub fn new(global_running: &'static AtomicBool) -> Arc<Self> {
        let mons = enumerate_monitors();
        Arc::new(Self {
            global_running,
            generation: AtomicU64::new(0),
            is_animating: AtomicBool::new(false),
            active_windows: AtomicU64::new(0),
            extend_total_ms: AtomicU64::new(0),
            queue: Mutex::new(VecDeque::new()),
            next_event_seq: AtomicU64::new(0),
            events: Mutex::new(VecDeque::with_capacity(64)),
            total_triggers: AtomicU64::new(0),
            completed_count: AtomicU64::new(0),
            dropped_count: AtomicU64::new(0),
            queued_count: AtomicU64::new(0),
            extended_count: AtomicU64::new(0),
            restarted_count: AtomicU64::new(0),
            last_start_latency_us: AtomicU64::new(0),
            last_duration_ms: AtomicU64::new(0),
            last_frame_count: AtomicU64::new(0),
            last_avg_frame_us: AtomicU64::new(0),
            windows_created: AtomicU64::new(0),
            windows_destroyed: AtomicU64::new(0),
            dibs_created: AtomicU64::new(0),
            dibs_destroyed: AtomicU64::new(0),
            monitor_changes: AtomicU64::new(0),
            monitors_detected: AtomicU64::new(mons.len() as u64),
        })
    }

    pub fn push_event(
        &self,
        kind: &str,
        style: String,
        color_hex: String,
        duration_ms: i32,
        intensity: f64,
        monitors: usize,
    ) -> u64 {
        let seq = self.next_event_seq.fetch_add(1, Ordering::SeqCst) + 1;
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_millis() as i64)
            .unwrap_or(0);
        if let Ok(mut q) = self.events.lock() {
            if q.len() >= 128 {
                q.pop_front();
            }
            q.push_back(GlowEventRecord {
                seq,
                kind: kind.to_string(),
                style,
                color_hex,
                duration_ms,
                intensity,
                monitors,
                timestamp_unix_ms: ts,
            });
        }
        seq
    }

    pub fn drain_events(&self, ack_seq: u64, max_count: usize) -> (Vec<GlowEventRecord>, u64) {
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

    pub fn notify_monitor_changed(&self) -> usize {
        let mons = enumerate_monitors();
        let count = mons.len();
        self.monitors_detected.store(count as u64, Ordering::SeqCst);
        self.monitor_changes.fetch_add(1, Ordering::Relaxed);
        // Safely stop any active overlay targeting stale monitor geometry so next glow uses fresh bounds/DPI
        if self.is_animating.load(Ordering::SeqCst) {
            self.stop_all();
        }
        self.push_event(
            "MONITOR_CHANGED",
            String::new(),
            String::new(),
            0,
            0.0,
            count,
        );
        count
    }

    pub fn active_window_count(&self) -> u64 {
        self.active_windows.load(Ordering::SeqCst)
    }

    pub fn is_animating(&self) -> bool {
        self.is_animating.load(Ordering::SeqCst)
    }

    pub fn stop_all(&self) {
        let was_animating = self.is_animating.swap(false, Ordering::SeqCst);
        if let Ok(mut q) = self.queue.lock() {
            q.clear();
        }
        self.generation.fetch_add(1, Ordering::SeqCst);
        if was_animating {
            self.push_event("GLOW_STOPPED", String::new(), String::new(), 0, 0.0, 0);
        }
    }

    pub fn trigger(
        self: &Arc<Self>,
        original: &AppProfile,
        settings: &AppSettings,
        is_game_running: bool,
        active_game_id: Option<&str>,
        is_test: bool,
        dump_bmp_path: Option<String>,
    ) -> GlowTriggerOutcome {
        let decision = evaluate_and_adjust_profile(
            original,
            settings,
            is_game_running,
            active_game_id,
            is_test,
        );
        let (adjusted, reduce_motion_static) = match decision {
            TriggerDecision::Suppressed(reason) => {
                self.dropped_count.fetch_add(1, Ordering::Relaxed);
                return GlowTriggerOutcome {
                    action: reason,
                    style: format!("{:?}", original.Style),
                    duration_ms: original.DurationMs,
                    intensity: original.Intensity,
                    corner_radius: original.CornerRadius,
                    monitors: 0,
                };
            }
            TriggerDecision::Play {
                adjusted,
                reduce_motion_static,
            } => (adjusted, reduce_motion_static),
        };

        let req = GlowRequest {
            profile: adjusted.clone(),
            reduce_motion_static,
            monitor_mode: settings.MonitorMode,
            dump_bmp_path,
        };

        if self.is_animating.load(Ordering::SeqCst) {
            match settings.BurstMode {
                BurstMode::Ignore => {
                    self.dropped_count.fetch_add(1, Ordering::Relaxed);
                    return GlowTriggerOutcome {
                        action: "IGNORED_BURST",
                        style: format!("{:?}", adjusted.Style),
                        duration_ms: adjusted.DurationMs,
                        intensity: adjusted.Intensity,
                        corner_radius: adjusted.CornerRadius,
                        monitors: 0,
                    };
                }
                BurstMode::Queue => {
                    if let Ok(mut q) = self.queue.lock() {
                        q.push_back(req);
                    }
                    self.queued_count.fetch_add(1, Ordering::Relaxed);
                    return GlowTriggerOutcome {
                        action: "QUEUED_BURST",
                        style: format!("{:?}", adjusted.Style),
                        duration_ms: adjusted.DurationMs,
                        intensity: adjusted.Intensity,
                        corner_radius: adjusted.CornerRadius,
                        monitors: 0,
                    };
                }
                BurstMode::Extend => {
                    // C# GlowManager.cs parity: apply new profile's color/style/settings and extend
                    // without clearing pending queue or spawning duplicate overlay sessions.
                    self.extended_count.fetch_add(1, Ordering::Relaxed);
                    let mon_count = self.launch_request(req);
                    return GlowTriggerOutcome {
                        action: "EXTENDED_BURST",
                        style: format!("{:?}", adjusted.Style),
                        duration_ms: adjusted.DurationMs,
                        intensity: adjusted.Intensity,
                        corner_radius: adjusted.CornerRadius,
                        monitors: mon_count,
                    };
                }
                BurstMode::Restart => {
                    self.restarted_count.fetch_add(1, Ordering::Relaxed);
                    self.generation.fetch_add(1, Ordering::SeqCst);
                }
            }
        }

        let mon_count = self.launch_request(req);
        GlowTriggerOutcome {
            action: "STARTED",
            style: format!("{:?}", adjusted.Style),
            duration_ms: adjusted.DurationMs,
            intensity: adjusted.Intensity,
            corner_radius: adjusted.CornerRadius,
            monitors: mon_count,
        }
    }

    fn launch_request(self: &Arc<Self>, req: GlowRequest) -> usize {
        self.total_triggers.fetch_add(1, Ordering::Relaxed);
        let gen = self.generation.fetch_add(1, Ordering::SeqCst) + 1;
        let effective_duration_ms =
            ((req.profile.DurationMs as f64) / req.profile.Speed.max(0.5)).max(500.0) as u64;
        self.extend_total_ms
            .store(effective_duration_ms, Ordering::SeqCst);
        self.is_animating.store(true, Ordering::SeqCst);

        let all_mons = enumerate_monitors();
        self.monitors_detected
            .store(all_mons.len() as u64, Ordering::Relaxed);
        let mut cursor = POINT::default();
        unsafe {
            let _ = GetCursorPos(&mut cursor);
        }
        let targets = select_target_monitors(req.monitor_mode, cursor.x, cursor.y, &all_mons);
        let target_count = targets.len();
        let style_str = format!("{:?}", req.profile.Style);
        let color_hex = req.profile.ColorHex.clone();
        let dur_ms = req.profile.DurationMs;
        let intensity = req.profile.Intensity;
        self.push_event(
            "GLOW_STARTED",
            style_str.clone(),
            color_hex.clone(),
            dur_ms,
            intensity,
            target_count,
        );

        let engine = Arc::clone(self);
        thread::spawn(move || {
            let t_start = Instant::now();
            let mut handles = Vec::with_capacity(targets.len());
            for mon in targets {
                let eng = Arc::clone(&engine);
                let req_clone = req.clone();
                handles.push(thread::spawn(move || {
                    render_glow_on_monitor(&eng, gen, mon, &req_clone, t_start);
                }));
            }
            for h in handles {
                let _ = h.join();
            }

            engine
                .last_duration_ms
                .store(t_start.elapsed().as_millis() as u64, Ordering::Relaxed);

            // Only advance queue if we are still the latest generation and app is running
            if engine.generation.load(Ordering::SeqCst) == gen {
                engine.completed_count.fetch_add(1, Ordering::Relaxed);
                let next_req = if engine.global_running.load(Ordering::SeqCst) {
                    engine.queue.lock().ok().and_then(|mut q| q.pop_front())
                } else {
                    None
                };

                if let Some(nr) = next_req {
                    engine.launch_request(nr);
                } else {
                    engine.is_animating.store(false, Ordering::SeqCst);
                    engine.push_event(
                        "GLOW_STOPPED",
                        style_str,
                        color_hex,
                        dur_ms,
                        intensity,
                        target_count,
                    );
                }
            }
        });

        target_count
    }
}

fn to_wide_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

unsafe extern "system" fn glow_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    DefWindowProcW(hwnd, msg, wparam, lparam)
}

#[inline]
fn lerp_u8(a: u8, b: u8, t: f64) -> u8 {
    let t = t.clamp(0.0, 1.0);
    (a as f64 + (b as f64 - a as f64) * t).round().clamp(0.0, 255.0) as u8
}

#[inline]
pub fn sample_rgb_spectrum(t: f64) -> (u8, u8, u8) {
    let wrapped = t.rem_euclid(1.0);
    let n = (RGB_SPECTRUM_COLORS.len() - 1) as f64;
    let scaled = wrapped * n;
    let idx = (scaled.floor() as usize).min(RGB_SPECTRUM_COLORS.len() - 2);
    let frac = scaled - idx as f64;
    let c0 = RGB_SPECTRUM_COLORS[idx];
    let c1 = RGB_SPECTRUM_COLORS[idx + 1];
    (
        lerp_u8(c0.0, c1.0, frac),
        lerp_u8(c0.1, c1.1, frac),
        lerp_u8(c0.2, c1.2, frac),
    )
}

#[inline]
fn sample_stops(stops: &[(f64, u8, u8, u8, u8)], offset: f64) -> (u8, u8, u8, u8) {
    if offset <= stops[0].0 {
        let s = stops[0];
        return (s.1, s.2, s.3, s.4);
    }
    let last = stops[stops.len() - 1];
    if offset >= last.0 {
        return (last.1, last.2, last.3, last.4);
    }
    for w in stops.windows(2) {
        let s0 = w[0];
        let s1 = w[1];
        if offset >= s0.0 && offset <= s1.0 {
            let span = (s1.0 - s0.0).max(1e-6);
            let t = (offset - s0.0) / span;
            return (
                lerp_u8(s0.1, s1.1, t),
                lerp_u8(s0.2, s1.2, t),
                lerp_u8(s0.3, s1.3, t),
                lerp_u8(s0.4, s1.4, t),
            );
        }
    }
    (0, 0, 0, 0)
}

#[inline]
fn ease_sine_inout(t: f64) -> f64 {
    0.5 * (1.0 - (std::f64::consts::PI * t.clamp(0.0, 1.0)).cos())
}

#[inline]
fn ease_quad_out(t: f64) -> f64 {
    let t = t.clamp(0.0, 1.0);
    1.0 - (1.0 - t) * (1.0 - t)
}

#[inline]
fn ease_quad_in(t: f64) -> f64 {
    let t = t.clamp(0.0, 1.0);
    t * t
}

/// Exact port of GlowBorderControl opacity keyframe curves
pub fn evaluate_opacity_envelope(
    style: GlowStyle,
    reduce_motion_static: bool,
    p: f64,
    max_opacity: f64,
) -> f64 {
    let p = p.clamp(0.0, 1.0);
    if reduce_motion_static {
        // Static clean fade: 0 -> max (12%) -> max (88%) -> 0 (100%)
        if p < 0.12 {
            return max_opacity * (p / 0.12);
        } else if p <= 0.88 {
            return max_opacity;
        } else {
            return max_opacity * ((1.0 - p) / 0.12);
        }
    }

    match style {
        GlowStyle::Pulse => {
            // 0 -> 1.0 (0.15) -> 0.25 (0.35) -> 1.0 (0.55) -> 0.25 (0.75) -> 0.90 (0.88) -> 0.0 (1.0)
            if p < 0.15 {
                max_opacity * ease_quad_out(p / 0.15)
            } else if p < 0.35 {
                let t = ease_sine_inout((p - 0.15) / 0.20);
                max_opacity * (1.0 - 0.75 * t)
            } else if p < 0.55 {
                let t = ease_sine_inout((p - 0.35) / 0.20);
                max_opacity * (0.25 + 0.75 * t)
            } else if p < 0.75 {
                let t = ease_sine_inout((p - 0.55) / 0.20);
                max_opacity * (1.0 - 0.75 * t)
            } else if p < 0.88 {
                let t = ease_sine_inout((p - 0.75) / 0.13);
                max_opacity * (0.25 + 0.65 * t)
            } else {
                let t = ease_quad_in((p - 0.88) / 0.12);
                max_opacity * 0.90 * (1.0 - t)
            }
        }
        GlowStyle::Ambient => {
            // 0 -> 0.85 (0.30) -> 0.70 (0.70) -> 0.0 (1.0)
            if p < 0.30 {
                max_opacity * 0.85 * ((p / 0.30) * std::f64::consts::FRAC_PI_2).sin()
            } else if p < 0.70 {
                let t = ease_sine_inout((p - 0.30) / 0.40);
                max_opacity * (0.85 - 0.15 * t)
            } else {
                let t = ease_quad_in((p - 0.70) / 0.30);
                max_opacity * 0.70 * (1.0 - t)
            }
        }
        GlowStyle::Sweep => {
            if p < 0.10 {
                max_opacity * (p / 0.10)
            } else if p <= 0.90 {
                max_opacity
            } else {
                max_opacity * ((1.0 - p) / 0.10)
            }
        }
        GlowStyle::Comet => {
            if p < 0.08 {
                max_opacity * (p / 0.08)
            } else if p <= 0.92 {
                max_opacity
            } else {
                max_opacity * ((1.0 - p) / 0.08)
            }
        }
        GlowStyle::Ripple => {
            if p < 0.12 {
                max_opacity * (p / 0.12)
            } else if p <= 0.70 {
                let t = (p - 0.12) / 0.58;
                max_opacity * (1.0 - 0.10 * t)
            } else {
                max_opacity * 0.90 * ((1.0 - p) / 0.30)
            }
        }
    }
}

/// Signed distance inward from a rounded screen rectangle of size (w, h) with corner radius `cr`.
/// Returns negative outside the rounded corner (clipped), 0 at the rounded edge, positive inward.
#[inline]
pub fn rounded_border_inward_dist(x: f64, y: f64, w: f64, h: f64, cr: f64) -> f64 {
    if cr <= 0.5 {
        return x.min(w - 1.0 - x).min(y.min(h - 1.0 - y));
    }
    let hw = w * 0.5;
    let hh = h * 0.5;
    let r = cr.min(hw).min(hh);
    let px = (x - hw).abs() - (hw - r);
    let py = (y - hh).abs() - (hh - r);
    let outside = (px.max(0.0).hypot(py.max(0.0))) + px.max(py).min(0.0) - r;
    -outside
}

#[inline]
fn pack_premul_bgra(a: u8, r: u8, g: u8, b: u8) -> u32 {
    let af = a as u32;
    let pr = (r as u32 * af) / 255;
    let pg = (g as u32 * af) / 255;
    let pb = (b as u32 * af) / 255;
    (af << 24) | (pr << 16) | (pg << 8) | pb
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct StripRect {
    /// Offset relative to monitor top-left (0..mon.width, 0..mon.height)
    pub off_x: i32,
    pub off_y: i32,
    pub width: i32,
    pub height: i32,
}

/// Computes four strictly non-overlapping edge strips (Top, Bottom, Left, Right)
/// covering every pixel within `band_px` of the monitor edges (including full rounded corners).
/// Falls back to a single full-monitor rect if `2 * band_px` covers the monitor.
pub fn compute_four_strip_rects(mon_w: i32, mon_h: i32, band_px: i32) -> Vec<StripRect> {
    if mon_w <= 0 || mon_h <= 0 {
        return Vec::new();
    }
    let s = band_px.max(1).min(mon_w / 2).min(mon_h / 2);
    if s * 2 >= mon_h || s * 2 >= mon_w {
        return vec![StripRect {
            off_x: 0,
            off_y: 0,
            width: mon_w,
            height: mon_h,
        }];
    }
    let mid_h = mon_h - 2 * s;
    vec![
        // Top (full width, owns top-left & top-right corners)
        StripRect {
            off_x: 0,
            off_y: 0,
            width: mon_w,
            height: s,
        },
        // Bottom (full width, owns bottom-left & bottom-right corners)
        StripRect {
            off_x: 0,
            off_y: mon_h - s,
            width: mon_w,
            height: s,
        },
        // Left (strictly between top and bottom — zero overlap)
        StripRect {
            off_x: 0,
            off_y: s,
            width: s,
            height: mid_h,
        },
        // Right (strictly between top and bottom — zero overlap)
        StripRect {
            off_x: mon_w - s,
            off_y: s,
            width: s,
            height: mid_h,
        },
    ]
}

fn fill_base_border_region(
    pixels: &mut [u32],
    strip_w: i32,
    strip_h: i32,
    off_x: i32,
    off_y: i32,
    mon_w: i32,
    mon_h: i32,
    border_px: f64,
    thickness_px: f64,
    corner_radius_px: f64,
    base_color: (u8, u8, u8),
    is_rgb: bool,
    opacity_scale: f64,
) {
    let wf = mon_w as f64;
    let hf = mon_h as f64;
    let full_surface = off_x == 0 && off_y == 0 && strip_w == mon_w && strip_h == mon_h;
    let b_int = (border_px.ceil() as i32 + corner_radius_px.ceil() as i32 + 2)
        .min(mon_w / 2)
        .min(mon_h / 2);

    for ly in 0..strip_h {
        let gy = off_y + ly;
        let in_y_strip = !full_surface || gy < b_int || gy >= mon_h - b_int;
        let row_off = (ly * strip_w) as usize;
        for lx in 0..strip_w {
            let gx = off_x + lx;
            if full_surface && !in_y_strip && gx >= b_int && gx < mon_w - b_int {
                continue;
            }
            let d = rounded_border_inward_dist(gx as f64, gy as f64, wf, hf, corner_radius_px);
            if d < 0.0 || d >= border_px {
                continue;
            }
            let alpha_f = if d < thickness_px {
                1.0
            } else {
                let t = 1.0 - ((d - thickness_px) / (border_px - thickness_px).max(1.0));
                t * t
            };
            let a = (alpha_f * 255.0 * opacity_scale).clamp(0.0, 255.0) as u8;
            if a == 0 {
                continue;
            }
            let (r, g, b) = if is_rgb {
                let u = ((gx as f64 / wf) + (gy as f64 / hf)) * 0.5;
                sample_rgb_spectrum(u)
            } else {
                base_color
            };
            pixels[row_off + lx as usize] = pack_premul_bgra(a, r, g, b);
        }
    }
}

fn fill_base_border_pixels(
    pixels: &mut [u32],
    w: i32,
    h: i32,
    border_px: f64,
    thickness_px: f64,
    corner_radius_px: f64,
    base_color: (u8, u8, u8),
    is_rgb: bool,
    opacity_scale: f64,
) {
    fill_base_border_region(
        pixels,
        w,
        h,
        0,
        0,
        w,
        h,
        border_px,
        thickness_px,
        corner_radius_px,
        base_color,
        is_rgb,
        opacity_scale,
    );
}

fn render_animated_border_region(
    pixels: &mut [u32],
    strip_w: i32,
    strip_h: i32,
    off_x: i32,
    off_y: i32,
    mon_w: i32,
    mon_h: i32,
    border_px: f64,
    thickness_px: f64,
    dpi_scale: f64,
    corner_radius_px: f64,
    base_color: (u8, u8, u8),
    is_rgb: bool,
    style: GlowStyle,
    cycle_progress: f64,
) {
    let wf = mon_w as f64;
    let hf = mon_h as f64;
    let full_surface = off_x == 0 && off_y == 0 && strip_w == mon_w && strip_h == mon_h;
    // Exact C# GlowBorderControl two-layer geometry:
    // - Core border: max(8, Thickness * 2.2) * dpi_scale
    // - Bloom border: Sweep -> max(26, GlowSize) * dpi_scale; Comet -> max(28, GlowSize * 1.1) * dpi_scale
    let core_px = (thickness_px * 2.2).max(8.0 * dpi_scale);
    let bloom_px = match style {
        GlowStyle::Sweep => border_px.max(26.0 * dpi_scale),
        GlowStyle::Comet => (border_px * 1.1).max(28.0 * dpi_scale),
        _ => border_px,
    };
    let max_span_px = border_px.max(bloom_px).max(core_px);
    let b_int = (max_span_px.ceil() as i32 + corner_radius_px.ceil() as i32 + 2)
        .min(mon_w / 2)
        .min(mon_h / 2);

    // Precompute stops for Sweep / Comet (matching GlowBorderControl + GlowSpectrumBrushFactory)
    let (start_x, start_y, dx, dy, inv_len2) = match style {
        GlowStyle::Sweep => {
            let pos = -0.8 + 2.4 * cycle_progress;
            let dx = 0.6;
            let dy = 0.6;
            (pos, pos, dx, dy, 1.0 / (dx * dx + dy * dy))
        }
        GlowStyle::Comet => {
            let cp = if cycle_progress <= 0.5 {
                cycle_progress * 2.0
            } else {
                (1.0 - cycle_progress) * 2.0
            };
            let pos = -0.4 + 1.6 * cp;
            let dx = 0.8;
            let dy = 0.6;
            (pos, pos, dx, dy, 1.0 / (dx * dx + dy * dy))
        }
        _ => (0.0, 0.0, 1.0, 1.0, 0.5),
    };

    let (cr, cg, cb) = base_color;
    let sweep_core_solid: [(f64, u8, u8, u8, u8); 7] = [
        (0.0, 0, cr, cg, cb),
        (0.20, 60, cr, cg, cb),
        (0.40, 200, cr, cg, cb),
        (0.50, 255, 255, 255, 255),
        (0.60, 200, cr, cg, cb),
        (0.80, 60, cr, cg, cb),
        (1.0, 0, cr, cg, cb),
    ];
    let sweep_bloom_solid: [(f64, u8, u8, u8, u8); 5] = [
        (0.0, 0, cr, cg, cb),
        (0.30, 80, cr, cg, cb),
        (0.50, 180, cr, cg, cb),
        (0.70, 80, cr, cg, cb),
        (1.0, 0, cr, cg, cb),
    ];
    let sweep_core_rgb: [(f64, u8, u8, u8, u8); 7] = [
        (0.0, 0, 255, 0, 77),
        (0.15, 140, 255, 0, 77),
        (0.35, 255, 255, 214, 0),
        (0.50, 255, 255, 255, 255),
        (0.65, 255, 0, 229, 255),
        (0.85, 150, 124, 58, 237),
        (1.0, 0, 124, 58, 237),
    ];
    let sweep_bloom_rgb: [(f64, u8, u8, u8, u8); 5] = [
        (0.0, 0, 255, 0, 77),
        (0.20, 90, 255, 0, 77),
        (0.50, 190, 0, 230, 118),
        (0.80, 90, 124, 58, 237),
        (1.0, 0, 124, 58, 237),
    ];
    let comet_core_solid: [(f64, u8, u8, u8, u8); 7] = [
        (0.0, 0, cr, cg, cb),
        (0.25, 30, cr, cg, cb),
        (0.55, 120, cr, cg, cb),
        (0.82, 220, cr, cg, cb),
        (0.93, 255, cr, cg, cb),
        (0.98, 255, 255, 255, 255),
        (1.0, 0, cr, cg, cb),
    ];
    let comet_bloom_solid: [(f64, u8, u8, u8, u8); 5] = [
        (0.0, 0, cr, cg, cb),
        (0.40, 60, cr, cg, cb),
        (0.80, 180, cr, cg, cb),
        (0.96, 230, cr, cg, cb),
        (1.0, 0, cr, cg, cb),
    ];
    let comet_core_rgb: [(f64, u8, u8, u8, u8); 8] = [
        (0.0, 0, 124, 58, 237),
        (0.25, 60, 124, 58, 237),
        (0.50, 160, 41, 121, 255),
        (0.72, 255, 0, 230, 118),
        (0.86, 255, 255, 214, 0),
        (0.94, 255, 255, 0, 77),
        (0.98, 255, 255, 255, 255),
        (1.0, 0, 255, 255, 255),
    ];
    let comet_bloom_rgb: [(f64, u8, u8, u8, u8); 5] = [
        (0.0, 0, 124, 58, 237),
        (0.50, 80, 41, 121, 255),
        (0.85, 180, 255, 214, 0),
        (0.96, 240, 255, 0, 77),
        (1.0, 0, 255, 0, 77),
    ];

    for ly in 0..strip_h {
        let gy = off_y + ly;
        let in_y_strip = !full_surface || gy < b_int || gy >= mon_h - b_int;
        let row_off = (ly * strip_w) as usize;
        let ny = gy as f64 / hf;
        for lx in 0..strip_w {
            let gx = off_x + lx;
            if full_surface && !in_y_strip && gx >= b_int && gx < mon_w - b_int {
                continue;
            }
            let d = rounded_border_inward_dist(gx as f64, gy as f64, wf, hf, corner_radius_px);
            if d < 0.0 || d >= max_span_px {
                pixels[row_off + lx as usize] = 0;
                continue;
            }

            let nx = gx as f64 / wf;
            let (br, bg, bb) = if is_rgb {
                sample_rgb_spectrum((nx + ny) * 0.5)
            } else {
                base_color
            };

            // Layer 1: BaseGlowLayer at 0.20 opacity (within border_px)
            let mut out_af = if d < border_px {
                let ef = if d < thickness_px {
                    1.0
                } else {
                    let t = 1.0 - ((d - thickness_px) / (border_px - thickness_px).max(1.0));
                    t * t
                };
                ef * 0.20
            } else {
                0.0
            };
            let mut out_r = br as f64 * out_af;
            let mut out_g = bg as f64 * out_af;
            let mut out_b = bb as f64 * out_af;

            // Project normalized (nx, ny) onto moving gradient vector
            let proj = ((nx - start_x) * dx + (ny - start_y) * dy) * inv_len2;
            if (0.0..=1.0).contains(&proj) {
                // Layer 2: Outer Bloom Border (SweepBloomBorder / CometBloomBorder)
                if d < bloom_px {
                    let (ba, blr, blg, blb) = match (style, is_rgb) {
                        (GlowStyle::Sweep, true) => sample_stops(&sweep_bloom_rgb, proj),
                        (GlowStyle::Sweep, false) => sample_stops(&sweep_bloom_solid, proj),
                        (GlowStyle::Comet, true) => sample_stops(&comet_bloom_rgb, proj),
                        (GlowStyle::Comet, false) => sample_stops(&comet_bloom_solid, proj),
                        _ => (0, 0, 0, 0),
                    };
                    if ba > 0 {
                        let bloom_falloff = if d < core_px {
                            1.0
                        } else {
                            let t = 1.0 - ((d - core_px) / (bloom_px - core_px).max(1.0));
                            t * t
                        };
                        let sa = (ba as f64 / 255.0) * bloom_falloff;
                        let inv = 1.0 - sa;
                        out_r = blr as f64 * sa + out_r * inv;
                        out_g = blg as f64 * sa + out_g * inv;
                        out_b = blb as f64 * sa + out_b * inv;
                        out_af = sa + out_af * inv;
                    }
                }

                // Layer 3: Sharp Core Border (SweepOverlay / CometOverlay)
                if d < core_px {
                    let (ca, cor, cog, cob) = match (style, is_rgb) {
                        (GlowStyle::Sweep, true) => sample_stops(&sweep_core_rgb, proj),
                        (GlowStyle::Sweep, false) => sample_stops(&sweep_core_solid, proj),
                        (GlowStyle::Comet, true) => sample_stops(&comet_core_rgb, proj),
                        (GlowStyle::Comet, false) => sample_stops(&comet_core_solid, proj),
                        _ => (0, 0, 0, 0),
                    };
                    if ca > 0 {
                        let sa = ca as f64 / 255.0;
                        let inv = 1.0 - sa;
                        out_r = cor as f64 * sa + out_r * inv;
                        out_g = cog as f64 * sa + out_g * inv;
                        out_b = cob as f64 * sa + out_b * inv;
                        out_af = sa + out_af * inv;
                    }
                }
            }

            let final_a = (out_af * 255.0).clamp(0.0, 255.0) as u8;
            if final_a == 0 {
                pixels[row_off + lx as usize] = 0;
            } else {
                let r = (out_r / out_af).clamp(0.0, 255.0) as u8;
                let g = (out_g / out_af).clamp(0.0, 255.0) as u8;
                let b = (out_b / out_af).clamp(0.0, 255.0) as u8;
                pixels[row_off + lx as usize] = pack_premul_bgra(final_a, r, g, b);
            }
        }
    }
}

#[allow(dead_code)]
fn render_animated_border_frame(
    pixels: &mut [u32],
    w: i32,
    h: i32,
    border_px: f64,
    thickness_px: f64,
    dpi_scale: f64,
    corner_radius_px: f64,
    base_color: (u8, u8, u8),
    is_rgb: bool,
    style: GlowStyle,
    cycle_progress: f64,
) {
    render_animated_border_region(
        pixels,
        w,
        h,
        0,
        0,
        w,
        h,
        border_px,
        thickness_px,
        dpi_scale,
        corner_radius_px,
        base_color,
        is_rgb,
        style,
        cycle_progress,
    );
}

fn render_ripple_frame(
    pixels: &mut [u32],
    w: i32,
    h: i32,
    border_px: f64,
    thickness_px: f64,
    corner_radius_px: f64,
    base_color: (u8, u8, u8),
    is_rgb: bool,
    cycle_progress: f64,
    prev_ring: &mut (i32, i32, i32, i32),
) {
    // Clear previous shockwave bounding box efficiently
    let (px0, py0, px1, py1) = *prev_ring;
    if px1 > px0 && py1 > py0 {
        for y in py0.max(0)..py1.min(h) {
            let row = (y * w) as usize;
            pixels[row + px0.max(0) as usize..row + px1.min(w) as usize].fill(0);
        }
    }

    // Re-fill subtle 0.20 base border
    fill_base_border_pixels(
        pixels,
        w,
        h,
        border_px,
        thickness_px,
        corner_radius_px,
        base_color,
        is_rgb,
        0.20,
    );

    let cx = w as f64 * 0.5;
    let cy = h as f64 * 0.5;
    let max_radius = (cx * cx + cy * cy).sqrt() * 1.10;
    let radius = 10.0 + (max_radius - 10.0) * cycle_progress;
    let inner_r = radius * 0.65;
    let inner_r2 = inner_r * inner_r;
    let outer_r2 = radius * radius;
    let inv_r = 1.0 / radius.max(1.0);

    let x0 = ((cx - radius).floor() as i32).clamp(0, w);
    let x1 = ((cx + radius).ceil() as i32 + 1).clamp(0, w);
    let y0 = ((cy - radius).floor() as i32).clamp(0, h);
    let y1 = ((cy + radius).ceil() as i32 + 1).clamp(0, h);
    *prev_ring = (x0, y0, x1, y1);

    let (cr, cg, cb) = base_color;
    let ripple_solid: [(f64, u8, u8, u8, u8); 7] = [
        (0.0, 0, cr, cg, cb),
        (0.65, 0, cr, cg, cb),
        (0.80, 100, cr, cg, cb),
        (0.88, 220, cr, cg, cb),
        (0.94, 255, 255, 255, 255),
        (0.97, 160, cr, cg, cb),
        (1.0, 0, cr, cg, cb),
    ];
    let ripple_rgb: [(f64, u8, u8, u8, u8); 9] = [
        (0.0, 0, 41, 121, 255),
        (0.65, 0, 41, 121, 255),
        (0.75, 160, 124, 58, 237),
        (0.83, 255, 0, 229, 255),
        (0.89, 255, 0, 230, 118),
        (0.94, 255, 255, 255, 255),
        (0.96, 255, 255, 214, 0),
        (0.98, 255, 255, 0, 77),
        (1.0, 0, 255, 0, 77),
    ];

    // Step by 2x2 blocks for ultra-low CPU on high-DPI displays while keeping crisp shockwave appearance
    let mut y = y0;
    while y < y1 {
        let dy = (y as f64) - cy;
        let dy2 = dy * dy;
        let row0 = (y * w) as usize;
        let has_next_y = (y + 1) < y1;
        let row1 = if has_next_y { ((y + 1) * w) as usize } else { row0 };

        let mut x = x0;
        while x < x1 {
            let dx = (x as f64) - cx;
            let d2 = dx * dx + dy2;
            if d2 >= inner_r2 && d2 <= outer_r2 {
                let off = d2.sqrt() * inv_r;
                let (a, r, g, b) = if is_rgb {
                    sample_stops(&ripple_rgb, off)
                } else {
                    sample_stops(&ripple_solid, off)
                };
                if a > 0 {
                    let px = pack_premul_bgra(a, r, g, b);
                    pixels[row0 + x as usize] = px;
                    if x + 1 < x1 {
                        pixels[row0 + (x + 1) as usize] = px;
                    }
                    if has_next_y {
                        pixels[row1 + x as usize] = px;
                        if x + 1 < x1 {
                            pixels[row1 + (x + 1) as usize] = px;
                        }
                    }
                }
            }
            x += 2;
        }
        y += 2;
    }
}

fn dump_premul_dib_to_bmp(path: &str, pixels: &[u32], w: i32, h: i32, const_alpha: u8) {
    // Downscale 2x so 2560x1600 dumps fast as a crisp 1280x800 24-bit BMP composited on dark #101218 backdrop
    let step = if w >= 1600 { 2 } else { 1 };
    let dw = (w / step) as usize;
    let dh = (h / step) as usize;
    let row_bytes = (dw * 3 + 3) & !3;
    let img_size = row_bytes * dh;
    let file_size = 54 + img_size;

    let mut out = Vec::with_capacity(file_size);
    // BMP header (14 bytes)
    out.extend_from_slice(b"BM");
    out.extend_from_slice(&(file_size as u32).to_le_bytes());
    out.extend_from_slice(&0u32.to_le_bytes());
    out.extend_from_slice(&54u32.to_le_bytes());
    // DIB header (40 bytes)
    out.extend_from_slice(&40u32.to_le_bytes());
    out.extend_from_slice(&(dw as i32).to_le_bytes());
    out.extend_from_slice(&(-(dh as i32)).to_le_bytes()); // top-down
    out.extend_from_slice(&1u16.to_le_bytes());
    out.extend_from_slice(&24u16.to_le_bytes());
    out.extend_from_slice(&0u32.to_le_bytes());
    out.extend_from_slice(&(img_size as u32).to_le_bytes());
    out.extend_from_slice(&2835u32.to_le_bytes());
    out.extend_from_slice(&2835u32.to_le_bytes());
    out.extend_from_slice(&0u32.to_le_bytes());
    out.extend_from_slice(&0u32.to_le_bytes());

    let ca = const_alpha as f64 / 255.0;
    let (bg_r, bg_g, bg_b) = (16.0, 18.0, 24.0);
    let mut row = vec![0u8; row_bytes];
    for dy in 0..dh {
        let sy = (dy as i32) * step;
        let src_row = (sy * w) as usize;
        for dx in 0..dw {
            let sx = (dx as i32) * step;
            let px = pixels[src_row + sx as usize];
            let a = ((px >> 24) & 0xFF) as f64 / 255.0 * ca;
            let pr = ((px >> 16) & 0xFF) as f64 * ca;
            let pg = ((px >> 8) & 0xFF) as f64 * ca;
            let pb = (px & 0xFF) as f64 * ca;
            let inv = (1.0 - a).clamp(0.0, 1.0);
            let r = (pr + bg_r * inv).clamp(0.0, 255.0) as u8;
            let g = (pg + bg_g * inv).clamp(0.0, 255.0) as u8;
            let b = (pb + bg_b * inv).clamp(0.0, 255.0) as u8;
            row[dx * 3] = b;
            row[dx * 3 + 1] = g;
            row[dx * 3 + 2] = r;
        }
        out.extend_from_slice(&row);
    }
    let _ = std::fs::write(path, out);
}

struct StripSurface {
    rect: StripRect,
    hwnd: HWND,
    hdc_mem: HDC,
    hbmp: windows::Win32::Graphics::Gdi::HBITMAP,
    old_obj: windows::Win32::Graphics::Gdi::HGDIOBJ,
    bits_ptr: *mut u32,
    len: usize,
}

fn render_glow_on_monitor(
    engine: &Arc<GlowEngine>,
    my_gen: u64,
    mon: MonitorRect,
    req: &GlowRequest,
    t_request: Instant,
) {
    unsafe {
        let hinst: HINSTANCE = GetModuleHandleW(PCWSTR::null()).unwrap().into();
        let class_name = to_wide_null("NotiGlow_Rust_LayeredGlow_v2");
        let wc = WNDCLASSEXW {
            cbSize: std::mem::size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(glow_wnd_proc),
            hInstance: hinst,
            lpszClassName: PCWSTR(class_name.as_ptr()),
            ..Default::default()
        };
        let _ = RegisterClassExW(&wc);

        let ex_style = WS_EX_LAYERED
            | WS_EX_TRANSPARENT
            | WS_EX_TOPMOST
            | WS_EX_TOOLWINDOW
            | WS_EX_NOACTIVATE;

        let scale = mon.dpi_scale();
        let is_rgb = is_rgb_spectrum(&req.profile.ColorHex);
        let base_color = parse_color_rgb(&req.profile.ColorHex);
        let border_px = (req.profile.GlowSize.clamp(4.0, 256.0) * scale).max(6.0);
        let thickness_px = (req.profile.Thickness.clamp(1.0, 32.0) * scale).max(2.0);
        let corner_radius_px = req.profile.CornerRadius.clamp(0.0, 48.0) * scale;
        let max_opacity = req.profile.Intensity.clamp(0.05, 1.0);
        let style = req.profile.Style;
        let static_only = req.reduce_motion_static
            || matches!(style, GlowStyle::Pulse | GlowStyle::Ambient);

        // Four-strip optimization applies to all edge-only modes (Pulse, Ambient, Sweep, Comet, RGB Spectrum, ReduceMotion).
        // Ripple renders an expanding center-screen shockwave, so it retains a single full-monitor surface.
        let use_four_strips = static_only || !matches!(style, GlowStyle::Ripple);
        let strip_rects = if use_four_strips {
            let core_px = (thickness_px * 2.2).max(8.0 * scale);
            let bloom_px = match style {
                GlowStyle::Sweep => border_px.max(26.0 * scale),
                GlowStyle::Comet => (border_px * 1.1).max(28.0 * scale),
                _ => border_px,
            };
            let max_span_px = border_px.max(bloom_px).max(core_px);
            let band_px = max_span_px.ceil() as i32 + corner_radius_px.ceil() as i32 + 2;
            compute_four_strip_rects(mon.width, mon.height, band_px)
        } else {
            vec![StripRect {
                off_x: 0,
                off_y: 0,
                width: mon.width,
                height: mon.height,
            }]
        };

        let hdc_screen = HDC(std::ptr::null_mut());
        let mut surfaces: Vec<StripSurface> = Vec::with_capacity(strip_rects.len());

        for rect in strip_rects {
            let hwnd = match CreateWindowExW(
                ex_style,
                PCWSTR(class_name.as_ptr()),
                PCWSTR::null(),
                WS_POPUP,
                mon.left + rect.off_x,
                mon.top + rect.off_y,
                rect.width,
                rect.height,
                HWND(std::ptr::null_mut()),
                None,
                hinst,
                None,
            ) {
                Ok(h) => h,
                Err(_) => continue,
            };
            engine.active_windows.fetch_add(1, Ordering::SeqCst);
            engine.windows_created.fetch_add(1, Ordering::Relaxed);

            let hdc_mem = CreateCompatibleDC(hdc_screen);
            let mut bmi: BITMAPINFO = std::mem::zeroed();
            bmi.bmiHeader.biSize = std::mem::size_of::<BITMAPINFOHEADER>() as u32;
            bmi.bmiHeader.biWidth = rect.width;
            bmi.bmiHeader.biHeight = -rect.height;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB.0;

            let mut bits_ptr: *mut std::ffi::c_void = std::ptr::null_mut();
            match CreateDIBSection(hdc_mem, &bmi, DIB_RGB_COLORS, &mut bits_ptr, None, 0) {
                Ok(hbmp) if !bits_ptr.is_null() => {
                    engine.dibs_created.fetch_add(1, Ordering::Relaxed);
                    let old_obj = SelectObject(hdc_mem, hbmp);
                    surfaces.push(StripSurface {
                        rect,
                        hwnd,
                        hdc_mem,
                        hbmp,
                        old_obj,
                        bits_ptr: bits_ptr as *mut u32,
                        len: (rect.width * rect.height) as usize,
                    });
                }
                Ok(hbmp) => {
                    engine.dibs_created.fetch_add(1, Ordering::Relaxed);
                    let _ = DeleteObject(hbmp);
                    engine.dibs_destroyed.fetch_add(1, Ordering::Relaxed);
                    let _ = DeleteDC(hdc_mem);
                    let _ = DestroyWindow(hwnd);
                    engine.windows_destroyed.fetch_add(1, Ordering::Relaxed);
                    engine.active_windows.fetch_sub(1, Ordering::SeqCst);
                }
                Err(_) => {
                    let _ = DeleteDC(hdc_mem);
                    let _ = DestroyWindow(hwnd);
                    engine.windows_destroyed.fetch_add(1, Ordering::Relaxed);
                    engine.active_windows.fetch_sub(1, Ordering::SeqCst);
                }
            }
        }

        if !surfaces.is_empty() {
            if static_only {
                for surf in &mut surfaces {
                    let pixels = std::slice::from_raw_parts_mut(surf.bits_ptr, surf.len);
                    fill_base_border_region(
                        pixels,
                        surf.rect.width,
                        surf.rect.height,
                        surf.rect.off_x,
                        surf.rect.off_y,
                        mon.width,
                        mon.height,
                        border_px,
                        thickness_px,
                        corner_radius_px,
                        base_color,
                        is_rgb,
                        1.0,
                    );
                }
            }

            for surf in &surfaces {
                let _ = ShowWindow(surf.hwnd, SW_SHOWNOACTIVATE);
            }
            engine
                .last_start_latency_us
                .store(t_request.elapsed().as_micros() as u64, Ordering::Relaxed);

            let pt_src = POINT { x: 0, y: 0 };
            let cycle_ms: f64 = match style {
                GlowStyle::Sweep => (1500.0 / req.profile.Speed.max(0.5)).max(1000.0),
                GlowStyle::Comet => (1300.0 / req.profile.Speed.max(0.5)).max(900.0),
                GlowStyle::Ripple => (1500.0 / req.profile.Speed.max(0.5)).max(1100.0),
                _ => 1500.0,
            };

            let mut prev_ring = (0i32, 0i32, 0i32, 0i32);
            let mut dumped = false;
            let frame_sleep = if static_only {
                Duration::from_millis(16)
            } else {
                Duration::from_millis(24)
            };

            let anim_start = Instant::now();
            let mut frame_count = 0u64;
            let mut total_frame_us = 0u64;
            while engine.global_running.load(Ordering::Relaxed)
                && engine.generation.load(Ordering::SeqCst) == my_gen
            {
                let frame_t0 = Instant::now();
                let total_ms = engine.extend_total_ms.load(Ordering::Relaxed).max(500) as f64;
                let elapsed = anim_start.elapsed().as_secs_f64() * 1000.0;
                if elapsed >= total_ms {
                    break;
                }

                let p = elapsed / total_ms;
                let env = evaluate_opacity_envelope(
                    style,
                    req.reduce_motion_static,
                    p,
                    max_opacity,
                );

                if !static_only {
                    let cyc_p = (elapsed % cycle_ms) / cycle_ms;
                    match style {
                        GlowStyle::Sweep | GlowStyle::Comet => {
                            for surf in &mut surfaces {
                                let pixels = std::slice::from_raw_parts_mut(surf.bits_ptr, surf.len);
                                render_animated_border_region(
                                    pixels,
                                    surf.rect.width,
                                    surf.rect.height,
                                    surf.rect.off_x,
                                    surf.rect.off_y,
                                    mon.width,
                                    mon.height,
                                    border_px,
                                    thickness_px,
                                    scale,
                                    corner_radius_px,
                                    base_color,
                                    is_rgb,
                                    style,
                                    cyc_p,
                                );
                            }
                        }
                        GlowStyle::Ripple => {
                            if let Some(surf) = surfaces.first_mut() {
                                let pixels = std::slice::from_raw_parts_mut(surf.bits_ptr, surf.len);
                                render_ripple_frame(
                                    pixels,
                                    mon.width,
                                    mon.height,
                                    border_px,
                                    thickness_px,
                                    corner_radius_px,
                                    base_color,
                                    is_rgb,
                                    cyc_p,
                                    &mut prev_ring,
                                );
                            }
                        }
                        _ => {}
                    }
                }

                let const_alpha = (env * 255.0).clamp(0.0, 255.0) as u8;
                if !dumped {
                    if let Some(ref dump_path) = req.dump_bmp_path {
                        let target_p = match style {
                            GlowStyle::Pulse => 0.16,
                            GlowStyle::Ambient => 0.30,
                            GlowStyle::Sweep => 0.42,
                            GlowStyle::Comet => 0.35,
                            GlowStyle::Ripple => 0.48,
                        };
                        if p >= target_p {
                            if surfaces.len() == 1
                                && surfaces[0].rect.width == mon.width
                                && surfaces[0].rect.height == mon.height
                            {
                                let pixels = std::slice::from_raw_parts(surfaces[0].bits_ptr, surfaces[0].len);
                                dump_premul_dib_to_bmp(dump_path, pixels, mon.width, mon.height, const_alpha);
                            } else {
                                let mut full_buf = vec![0u32; (mon.width * mon.height) as usize];
                                for surf in &surfaces {
                                    let sp = std::slice::from_raw_parts(surf.bits_ptr, surf.len);
                                    for ly in 0..surf.rect.height {
                                        let dst_row = ((surf.rect.off_y + ly) * mon.width + surf.rect.off_x) as usize;
                                        let src_row = (ly * surf.rect.width) as usize;
                                        full_buf[dst_row..dst_row + surf.rect.width as usize]
                                            .copy_from_slice(&sp[src_row..src_row + surf.rect.width as usize]);
                                    }
                                }
                                dump_premul_dib_to_bmp(dump_path, &full_buf, mon.width, mon.height, const_alpha);
                            }
                            dumped = true;
                        }
                    }
                }

                let blend = BLENDFUNCTION {
                    BlendOp: AC_SRC_OVER as u8,
                    BlendFlags: 0,
                    SourceConstantAlpha: const_alpha,
                    AlphaFormat: AC_SRC_ALPHA as u8,
                };
                for surf in &surfaces {
                    let pt_dst = POINT {
                        x: mon.left + surf.rect.off_x,
                        y: mon.top + surf.rect.off_y,
                    };
                    let sz = SIZE {
                        cx: surf.rect.width,
                        cy: surf.rect.height,
                    };
                    let _ = UpdateLayeredWindow(
                        surf.hwnd,
                        hdc_screen,
                        Some(&pt_dst),
                        Some(&sz),
                        surf.hdc_mem,
                        Some(&pt_src),
                        COLORREF(0),
                        Some(&blend),
                        ULW_ALPHA,
                    );
                }

                frame_count += 1;
                total_frame_us += frame_t0.elapsed().as_micros() as u64;
                thread::sleep(frame_sleep);
            }

            if frame_count > 0 {
                engine.last_frame_count.store(frame_count, Ordering::Relaxed);
                engine
                    .last_avg_frame_us
                    .store(total_frame_us / frame_count, Ordering::Relaxed);
            }
        }

        for surf in surfaces {
            SelectObject(surf.hdc_mem, surf.old_obj);
            let _ = DeleteObject(surf.hbmp);
            engine.dibs_destroyed.fetch_add(1, Ordering::Relaxed);
            let _ = DeleteDC(surf.hdc_mem);
            let _ = DestroyWindow(surf.hwnd);
            engine.windows_destroyed.fetch_add(1, Ordering::Relaxed);
            engine.active_windows.fetch_sub(1, Ordering::SeqCst);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_multi_monitor_selection_and_dpi() {
        let mons = vec![
            MonitorRect {
                left: 0,
                top: 0,
                width: 2560,
                height: 1600,
                dpi: 144,
                is_primary: true,
                device_name: "DISPLAY1".into(),
            },
            MonitorRect {
                left: 2560,
                top: 0,
                width: 1920,
                height: 1080,
                dpi: 96,
                is_primary: false,
                device_name: "DISPLAY2".into(),
            },
        ];

        let primary = select_target_monitors(MonitorMode::PrimaryMonitor, 3000, 500, &mons);
        assert_eq!(primary.len(), 1);
        assert_eq!(primary[0].device_name, "DISPLAY1");
        assert!((primary[0].dpi_scale() - 1.5).abs() < 1e-6);

        let active = select_target_monitors(MonitorMode::ActiveMonitor, 3000, 500, &mons);
        assert_eq!(active.len(), 1);
        assert_eq!(active[0].device_name, "DISPLAY2");
        assert!((active[0].dpi_scale() - 1.0).abs() < 1e-6);

        let all = select_target_monitors(MonitorMode::AllMonitors, 100, 100, &mons);
        assert_eq!(all.len(), 2);
    }

    #[test]
    fn test_corner_radius_sdf_clips_sharp_corner() {
        // Point (0, 0) on a 1000x1000 screen with corner_radius = 40 must be OUTSIDE (negative)
        let d_corner = rounded_border_inward_dist(0.0, 0.0, 1000.0, 1000.0, 40.0);
        assert!(d_corner < 0.0);
        // Point (500, 0) on top edge midpoint must be ON the edge (~0)
        let d_top = rounded_border_inward_dist(500.0, 0.0, 1000.0, 1000.0, 40.0);
        assert!(d_top.abs() < 1e-6);
    }

    #[test]
    fn test_opacity_envelopes() {
        // Pulse peaks at 0.15 and dips at 0.35
        let p_peak = evaluate_opacity_envelope(GlowStyle::Pulse, false, 0.15, 0.8);
        let p_dip = evaluate_opacity_envelope(GlowStyle::Pulse, false, 0.35, 0.8);
        assert!((p_peak - 0.8).abs() < 0.02);
        assert!((p_dip - 0.2).abs() < 0.02);

        // ReduceMotion holds constant max_opacity at midpoint 0.50
        let rm_mid = evaluate_opacity_envelope(GlowStyle::Pulse, true, 0.50, 0.8);
        assert!((rm_mid - 0.8).abs() < 1e-6);
    }

    #[test]
    fn test_all_six_glow_modes_render_pixels_and_dpi_scales() {
        let w = 160i32;
        let h = 100i32;
        let mut buf = vec![0u32; (w * h) as usize];

        // 1 & 2. Pulse & Ambient (static border)
        fill_base_border_pixels(&mut buf, w, h, 24.0, 4.0, 12.0, (0, 229, 255), false, 1.0);
        assert!(buf.iter().any(|&px| (px >> 24) > 200));
        // Center must remain transparent (click-through / unobstructed desktop)
        assert_eq!(buf[(50 * w + 80) as usize], 0);

        // 3. Sweep (two-layer core + bloom)
        buf.fill(0);
        render_animated_border_frame(&mut buf, w, h, 24.0, 4.0, 1.0, 8.0, (255, 80, 0), false, GlowStyle::Sweep, 0.45);
        assert!(buf.iter().any(|&px| (px >> 24) > 100));

        // 4. Comet (two-layer core + bloom)
        buf.fill(0);
        render_animated_border_frame(&mut buf, w, h, 24.0, 4.0, 1.0, 8.0, (0, 255, 120), false, GlowStyle::Comet, 0.35);
        assert!(buf.iter().any(|&px| (px >> 24) > 100));

        // 5. Ripple
        buf.fill(0);
        let mut prev_ring = (0, 0, 0, 0);
        render_ripple_frame(&mut buf, w, h, 24.0, 4.0, 0.0, (124, 58, 237), false, 0.50, &mut prev_ring);
        assert!(buf.iter().any(|&px| (px >> 24) > 100));

        // 6. RGB Spectrum
        buf.fill(0);
        fill_base_border_pixels(&mut buf, w, h, 24.0, 4.0, 0.0, (255, 0, 77), true, 1.0);
        let top_left = buf[(2 * w + 2) as usize];
        let bot_right = buf[((h - 3) * w + (w - 3)) as usize];
        assert!(top_left != 0 && bot_right != 0);

        // DPI scaling checks: 100%, 125%, 150%, 200%
        for (dpi, expected) in [(96u32, 1.0), (120, 1.25), (144, 1.5), (192, 2.0)] {
            let m = MonitorRect {
                left: 0,
                top: 0,
                width: 1920,
                height: 1080,
                dpi,
                is_primary: true,
                device_name: "D".into(),
            };
            assert!((m.dpi_scale() - expected).abs() < 1e-6);
        }
    }

    #[test]
    fn test_burst_modes_and_monitor_changed_event() {
        static RUN: AtomicBool = AtomicBool::new(true);
        let engine = GlowEngine::new(&RUN);
        let mut settings = AppSettings::default();
        let mut profile = AppProfile::default();
        profile.DurationMs = 500;

        // Simulate active animation to test BurstMode::Ignore / Queue / Extend without spawning Win32 windows
        engine.is_animating.store(true, Ordering::SeqCst);

        settings.BurstMode = BurstMode::Ignore;
        let out_ign = engine.trigger(&profile, &settings, false, None, false, None);
        assert_eq!(out_ign.action, "IGNORED_BURST");
        assert_eq!(engine.dropped_count.load(Ordering::Relaxed), 1);

        settings.BurstMode = BurstMode::Queue;
        let out_q = engine.trigger(&profile, &settings, false, None, false, None);
        assert_eq!(out_q.action, "QUEUED_BURST");
        assert_eq!(engine.queued_count.load(Ordering::Relaxed), 1);

        settings.BurstMode = BurstMode::Extend;
        let mut extend_profile = profile.clone();
        extend_profile.ColorHex = "#FF00AA".to_string();
        extend_profile.Style = GlowStyle::Ambient;
        let out_ext = engine.trigger(&extend_profile, &settings, false, None, false, None);
        assert_eq!(out_ext.action, "EXTENDED_BURST");
        assert_eq!(out_ext.style, "Ambient");
        assert_eq!(engine.extended_count.load(Ordering::Relaxed), 1);

        // Monitor change stops active overlay and emits MONITOR_CHANGED
        let mon_cnt = engine.notify_monitor_changed();
        assert!(mon_cnt >= 1);
        assert!(!engine.is_animating());
        let (evs, _) = engine.drain_events(0, 16);
        assert!(evs.iter().any(|e| e.kind == "GLOW_STARTED" && e.color_hex == "#FF00AA"));
        assert!(evs.iter().any(|e| e.kind == "MONITOR_CHANGED"));
    }

    #[test]
    fn test_four_strip_geometry_disjoint_negative_coords_and_pixel_parity() {
        // 1. Resolution & memory footprint verification: 1920x1080, 2560x1440, 2560x1600
        for (w, h, band) in [(1920, 1080, 70), (2560, 1440, 96), (2560, 1600, 104)] {
            let strips = compute_four_strip_rects(w, h, band);
            assert_eq!(strips.len(), 4);
            let full_bytes = (w as u64) * (h as u64) * 4;
            let strip_bytes: u64 = strips
                .iter()
                .map(|s| (s.width as u64) * (s.height as u64) * 4)
                .sum();
            // Verify > 70% bitmap memory reduction across all standard resolutions
            assert!(
                strip_bytes * 100 < full_bytes * 30,
                "{}x{}: expected strip_bytes ({}) < 30% of full_bytes ({})",
                w,
                h,
                strip_bytes,
                full_bytes
            );

            // Verify strips are strictly disjoint (no overlapping pixels, no double-blending)
            for i in 0..strips.len() {
                for j in (i + 1)..strips.len() {
                    let a = strips[i];
                    let b = strips[j];
                    let x_overlap = a.off_x < b.off_x + b.width && b.off_x < a.off_x + a.width;
                    let y_overlap = a.off_y < b.off_y + b.height && b.off_y < a.off_y + a.height;
                    assert!(!(x_overlap && y_overlap), "Strips {} and {} overlap!", i, j);
                }
            }
        }

        // 2. Degenerate fallback when 2*band >= height
        let tiny = compute_four_strip_rects(320, 200, 120);
        assert_eq!(tiny.len(), 1);
        assert_eq!(tiny[0], StripRect { off_x: 0, off_y: 0, width: 320, height: 200 });

        // 3. Bit-for-bit pixel parity: 4-strip vs full-screen across Pulse, Sweep, Comet, RGB Spectrum with rounded corners
        let w = 320;
        let h = 200;
        let border_px: f64 = 28.0;
        let thickness_px: f64 = 5.0;
        let cr: f64 = 18.0;
        let bloom_px = (border_px * 1.1).max(28.0);
        let core_px = (thickness_px * 2.2).max(8.0);
        let band = border_px.max(bloom_px).max(core_px).ceil() as i32 + cr.ceil() as i32 + 2;
        let strips = compute_four_strip_rects(w, h, band);
        assert_eq!(strips.len(), 4);

        // Static Pulse + RGB Spectrum parity
        for is_rgb in [false, true] {
            let mut full_buf = vec![0u32; (w * h) as usize];
            fill_base_border_pixels(&mut full_buf, w, h, border_px, thickness_px, cr, (0, 229, 255), is_rgb, 1.0);
            let mut covered = vec![false; (w * h) as usize];
            for s in &strips {
                let mut sbuf = vec![0u32; (s.width * s.height) as usize];
                fill_base_border_region(&mut sbuf, s.width, s.height, s.off_x, s.off_y, w, h, border_px, thickness_px, cr, (0, 229, 255), is_rgb, 1.0);
                for ly in 0..s.height {
                    for lx in 0..s.width {
                        let gx = s.off_x + lx;
                        let gy = s.off_y + ly;
                        let fidx = (gy * w + gx) as usize;
                        assert!(!covered[fidx], "Duplicate coverage at ({},{})", gx, gy);
                        covered[fidx] = true;
                        assert_eq!(sbuf[(ly * s.width + lx) as usize], full_buf[fidx]);
                    }
                }
            }
            for idx in 0..full_buf.len() {
                if !covered[idx] {
                    assert_eq!(full_buf[idx], 0, "Non-zero pixel outside 4 strips!");
                }
            }
        }

        // Dynamic Sweep & Comet parity (solid & RGB)
        for style in [GlowStyle::Sweep, GlowStyle::Comet] {
            for is_rgb in [false, true] {
                let mut full_buf = vec![0u32; (w * h) as usize];
                render_animated_border_frame(&mut full_buf, w, h, border_px, thickness_px, 1.0, cr, (0, 229, 255), is_rgb, style, 0.37);
                let mut covered = vec![false; (w * h) as usize];
                for s in &strips {
                    let mut sbuf = vec![0u32; (s.width * s.height) as usize];
                    render_animated_border_region(&mut sbuf, s.width, s.height, s.off_x, s.off_y, w, h, border_px, thickness_px, 1.0, cr, (0, 229, 255), is_rgb, style, 0.37);
                    for ly in 0..s.height {
                        for lx in 0..s.width {
                            let gx = s.off_x + lx;
                            let gy = s.off_y + ly;
                            let fidx = (gy * w + gx) as usize;
                            covered[fidx] = true;
                            assert_eq!(sbuf[(ly * s.width + lx) as usize], full_buf[fidx]);
                        }
                    }
                }
                for idx in 0..full_buf.len() {
                    if !covered[idx] {
                        assert_eq!(full_buf[idx], 0, "Non-zero animated pixel outside 4 strips!");
                    }
                }
            }
        }
    }
}
