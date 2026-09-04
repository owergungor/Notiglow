use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, OnceLock};

use crate::glow::GlowManager;
use crate::notification::NotificationEngine;
use crate::settings::SettingsStorage;

/// Global application state for Curry.
///
/// Holds cross-platform state such as the active/enabled flag and references
/// to core engine subsystems (NotificationEngine, GlowManager, SettingsStorage).
pub struct AppState {
    pub enabled: Arc<AtomicBool>,
    notification_engine: OnceLock<Arc<NotificationEngine>>,
    glow_manager: OnceLock<Arc<GlowManager>>,
    settings_storage: OnceLock<Arc<SettingsStorage>>,
}

impl Default for AppState {
    fn default() -> Self {
        Self::new()
    }
}

impl AppState {
    pub fn new() -> Self {
        Self {
            enabled: Arc::new(AtomicBool::new(true)),
            notification_engine: OnceLock::new(),
            glow_manager: OnceLock::new(),
            settings_storage: OnceLock::new(),
        }
    }

    pub fn is_enabled(&self) -> bool {
        self.enabled.load(Ordering::SeqCst)
    }

    pub fn set_enabled(&self, val: bool) {
        self.enabled.store(val, Ordering::SeqCst);
        if let Some(storage) = self.settings_storage.get() {
            let mut settings = storage.get();
            if settings.enabled != val {
                settings.enabled = val;
                let _ = storage.update(settings);
            }
        }
    }

    pub fn toggle_enabled(&self) -> bool {
        let current = self.enabled.load(Ordering::SeqCst);
        let next = !current;
        self.set_enabled(next);
        next
    }

    pub fn enabled_flag(&self) -> Arc<AtomicBool> {
        Arc::clone(&self.enabled)
    }

    /// Binds the NotificationEngine instance during startup.
    pub fn set_notification_engine(&self, engine: Arc<NotificationEngine>) {
        let _ = self.notification_engine.set(engine);
    }

    /// Accesses the NotificationEngine instance.
    pub fn notification_engine(&self) -> Option<&Arc<NotificationEngine>> {
        self.notification_engine.get()
    }

    /// Binds the GlowManager instance during startup.
    pub fn set_glow_manager(&self, manager: Arc<GlowManager>) {
        let _ = self.glow_manager.set(manager);
    }

    /// Accesses the GlowManager instance.
    pub fn glow_manager(&self) -> Option<&Arc<GlowManager>> {
        self.glow_manager.get()
    }

    /// Binds the SettingsStorage instance during startup.
    pub fn set_settings_storage(&self, storage: Arc<SettingsStorage>) {
        let _ = self.settings_storage.set(storage);
    }

    /// Accesses the SettingsStorage instance.
    pub fn settings_storage(&self) -> Option<&Arc<SettingsStorage>> {
        self.settings_storage.get()
    }
}
