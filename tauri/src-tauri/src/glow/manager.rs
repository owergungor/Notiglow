use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, OnceLock};
use tauri::{AppHandle, Emitter, Manager, WebviewWindow};

use crate::glow::model::{GlowPayload, GlowSettings, MonitorTarget};
use crate::glow::storage::GlowStorage;
use crate::settings::SettingsStorage;

/// Coordinates the screen-edge glow overlay window, settings, and animation lifecycle.
pub struct GlowManager {
    app_handle: AppHandle,
    storage: GlowStorage,
    settings_storage: OnceLock<Arc<SettingsStorage>>,
    active_generation: Arc<AtomicU64>,
}

impl GlowManager {
    /// Creates a new GlowManager instance.
    pub fn new(app: &AppHandle) -> Self {
        Self {
            app_handle: app.clone(),
            storage: GlowStorage::new(app),
            settings_storage: OnceLock::new(),
            active_generation: Arc::new(AtomicU64::new(0)),
        }
    }

    /// Binds the centralized SettingsStorage instance.
    pub fn set_settings_storage(&self, storage: Arc<SettingsStorage>) {
        let _ = self.settings_storage.set(storage);
    }

    /// Retrieves current glow settings from SettingsStorage if bound, or local storage.
    pub fn get_settings(&self) -> GlowSettings {
        if let Some(storage) = self.settings_storage.get() {
            storage.get().glow
        } else {
            self.storage.get()
        }
    }

    /// Updates glow settings and persists them to local configuration.
    pub fn update_settings(&self, settings: GlowSettings) -> Result<(), String> {
        if let Some(storage) = self.settings_storage.get() {
            let mut app_settings = storage.get();
            app_settings.glow = settings.clone();
            let _ = storage.update(app_settings)?;
        }
        self.storage.update(settings)
    }

    /// Triggers the screen-edge glow effect on the overlay window.
    pub fn trigger_glow(&self, custom_color: Option<&str>) {
        self.trigger_glow_internal(custom_color, None, None);
    }

    /// Triggers the screen-edge glow effect calibrated to a specific notification's urgency and duration.
    pub fn trigger_glow_for_notification(&self, notification: &crate::notification::model::Notification) {
        let settings = self.get_settings();
        if !settings.enabled {
            return;
        }

        // Play native sound alert if enabled in settings
        if let Some(storage) = self.settings_storage.get() {
            if storage.get().sound_enabled {
                crate::settings::SoundManager::play_alert();
            }
        }

        let (color, intensity_mult) = match notification.urgency {
            Some(crate::notification::model::NotificationUrgency::Critical) => ("#ef4444", 1.25),
            Some(crate::notification::model::NotificationUrgency::High) => ("#f59e0b", 1.15),
            Some(crate::notification::model::NotificationUrgency::Low) => (settings.color.as_str(), 0.75),
            _ => (settings.color.as_str(), 1.0),
        };

        let duration = notification.duration.unwrap_or(settings.duration_ms);
        let adjusted_intensity = (settings.intensity * intensity_mult).min(1.0);

        self.trigger_glow_internal(Some(color), Some(duration), Some(adjusted_intensity));
    }

    fn trigger_glow_internal(
        &self,
        custom_color: Option<&str>,
        custom_duration_ms: Option<u64>,
        custom_intensity: Option<f32>,
    ) {
        let settings = self.get_settings();
        if !settings.enabled {
            return;
        }

        let duration_ms = custom_duration_ms.unwrap_or(settings.duration_ms);
        let intensity = custom_intensity.unwrap_or(settings.intensity);

        let payload = GlowPayload {
            color: custom_color.unwrap_or(&settings.color).to_string(),
            duration_ms,
            intensity,
            thickness: settings.thickness,
            corner_radius: settings.corner_radius,
            animation_style: settings.animation_style,
        };

        match settings.monitor_target {
            MonitorTarget::Primary => {
                if let Some(w) = self.app_handle.get_webview_window("glow-overlay") {
                    if let Ok(Some(mon)) = w.primary_monitor() {
                        let _ = w.set_position(*mon.position());
                        let _ = w.set_size(*mon.size());
                    }
                    self.present_overlay(&w, &payload, duration_ms);
                }
            }
            MonitorTarget::Active => {
                if let Some(w) = self.app_handle.get_webview_window("glow-overlay") {
                    if let Some(mon) = find_active_monitor(&w) {
                        let _ = w.set_position(*mon.position());
                        let _ = w.set_size(*mon.size());
                    }
                    self.present_overlay(&w, &payload, duration_ms);
                }
            }
            MonitorTarget::All => {
                let monitors = self
                    .app_handle
                    .get_webview_window("glow-overlay")
                    .and_then(|w| w.available_monitors().ok())
                    .unwrap_or_default();

                if monitors.is_empty() {
                    if let Some(w) = self.app_handle.get_webview_window("glow-overlay") {
                        self.present_overlay(&w, &payload, duration_ms);
                    }
                } else {
                    for (idx, mon) in monitors.into_iter().enumerate() {
                        let win_label = if idx == 0 {
                            "glow-overlay".to_string()
                        } else {
                            format!("glow-overlay-{}", idx)
                        };

                        let window = if let Some(existing) = self.app_handle.get_webview_window(&win_label) {
                            Some(existing)
                        } else {
                            let url = tauri::WebviewUrl::App("/glow".into());
                            tauri::WebviewWindowBuilder::new(&self.app_handle, &win_label, url)
                                .title("Curry Overlay")
                                .transparent(true)
                                .decorations(false)
                                .always_on_top(true)
                                .skip_taskbar(true)
                                .visible(false)
                                .shadow(false)
                                .build()
                                .ok()
                        };

                        if let Some(w) = window {
                            let _ = w.set_position(*mon.position());
                            let _ = w.set_size(*mon.size());
                            self.present_overlay(&w, &payload, duration_ms);
                        }
                    }
                }
            }
        }
    }

    fn present_overlay(&self, window: &WebviewWindow, payload: &GlowPayload, duration_ms: u64) {
        let _ = window.set_ignore_cursor_events(true);
        let _ = window.set_always_on_top(true);

        #[cfg(target_os = "windows")]
        reinforce_windows_overlay(window);

        let _ = window.emit("trigger-glow", payload);
        let _ = window.show();

        let gen = self.active_generation.fetch_add(1, Ordering::SeqCst) + 1;
        let gen_arc = Arc::clone(&self.active_generation);
        let window_clone = window.clone();
        let total_duration = std::time::Duration::from_millis(duration_ms + 400);

        std::thread::spawn(move || {
            std::thread::sleep(total_duration);
            if gen_arc.load(Ordering::SeqCst) == gen {
                let _ = window_clone.hide();
            }
        });
    }
}

#[cfg(target_os = "windows")]
fn find_active_monitor(window: &WebviewWindow) -> Option<tauri::Monitor> {
    #[repr(C)]
    struct POINT {
        x: i32,
        y: i32,
    }
    extern "system" {
        fn GetCursorPos(lp_point: *mut POINT) -> i32;
    }

    let mut pt = POINT { x: 0, y: 0 };
    if unsafe { GetCursorPos(&mut pt) } != 0 {
        if let Ok(monitors) = window.available_monitors() {
            for mon in monitors {
                let pos = mon.position();
                let size = mon.size();
                if pt.x >= pos.x
                    && pt.x < pos.x + size.width as i32
                    && pt.y >= pos.y
                    && pt.y < pos.y + size.height as i32
                {
                    return Some(mon);
                }
            }
        }
    }

    window.primary_monitor().ok().flatten()
}

#[cfg(not(target_os = "windows"))]
fn find_active_monitor(window: &WebviewWindow) -> Option<tauri::Monitor> {
    window.current_monitor().ok().flatten().or_else(|| window.primary_monitor().ok().flatten())
}

#[cfg(target_os = "windows")]
fn reinforce_windows_overlay(window: &WebviewWindow) {
    if let Ok(hwnd_val) = window.hwnd() {
        let hwnd = hwnd_val.0 as isize;
        unsafe {
            extern "system" {
                fn GetWindowLongPtrW(hwnd: isize, n_index: i32) -> isize;
                fn SetWindowLongPtrW(hwnd: isize, n_index: i32, new_long: isize) -> isize;
                fn SetWindowPos(
                    hwnd: isize,
                    hwnd_insert_after: isize,
                    x: i32,
                    y: i32,
                    cx: i32,
                    cy: i32,
                    flags: u32,
                ) -> i32;
            }

            const GWL_EXSTYLE: i32 = -20;
            const WS_EX_TRANSPARENT: isize = 0x00000020;
            const WS_EX_LAYERED: isize = 0x00080000;
            const WS_EX_NOACTIVATE: isize = 0x08000000;
            const WS_EX_TOOLWINDOW: isize = 0x00000080;
            const HWND_TOPMOST: isize = -1;
            const SWP_NOSIZE: u32 = 0x0001;
            const SWP_NOMOVE: u32 = 0x0002;
            const SWP_NOACTIVATE: u32 = 0x0010;
            const SWP_SHOWWINDOW: u32 = 0x0040;

            let ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
            let new_style = ex_style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new_style);

            SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW,
            );
        }
    }
}
