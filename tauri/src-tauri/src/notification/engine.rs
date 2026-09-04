use std::collections::{HashSet, VecDeque};
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use serde::Serialize;
use tauri::{AppHandle, Emitter, Manager};

use crate::notification::model::Notification;
use crate::notification::platform::{
    create_platform_provider, NotificationError, NotificationProvider, ProviderStatus,
};
use crate::notification::storage::NotificationStorage;

/// Bounded FIFO set to track dismissed IDs, preventing unbounded memory growth.
#[derive(Debug)]
pub struct BoundedIdSet {
    order: VecDeque<String>,
    set: HashSet<String>,
    capacity: usize,
}

impl BoundedIdSet {
    pub fn new(capacity: usize) -> Self {
        Self {
            order: VecDeque::with_capacity(capacity),
            set: HashSet::with_capacity(capacity),
            capacity: capacity.max(1),
        }
    }

    pub fn insert(&mut self, id: String) {
        if self.set.contains(&id) {
            return;
        }
        if self.order.len() >= self.capacity {
            if let Some(old) = self.order.pop_front() {
                self.set.remove(&old);
            }
        }
        self.set.insert(id.clone());
        self.order.push_back(id);
    }

    pub fn contains(&self, id: &str) -> bool {
        self.set.contains(id)
    }

    pub fn len(&self) -> usize {
        self.set.len()
    }

    pub fn is_empty(&self) -> bool {
        self.set.is_empty()
    }
}

/// Serializable status summary of the notification pipeline.
#[derive(Debug, Clone, Serialize)]
pub struct PipelineStatus {
    pub provider_name: String,
    pub provider_status: ProviderStatus,
    pub captured_count: u64,
    pub is_enabled: bool,
}

/// Cross-platform notification engine.
///
/// Coordinates incoming notifications from platform providers, filters
/// them against application state and privacy preferences, stores recent
/// notifications in a bounded atomic persistent storage, emits Tauri events,
/// and triggers screen-edge glow effects.
pub struct NotificationEngine {
    app_handle: AppHandle,
    provider: Mutex<Box<dyn NotificationProvider>>,
    storage: Arc<NotificationStorage>,
    enabled: Arc<AtomicBool>,
    captured_count: AtomicU64,
    glow_manager: std::sync::OnceLock<Arc<crate::glow::GlowManager>>,
    settings_storage: std::sync::OnceLock<Arc<crate::settings::SettingsStorage>>,
    dismissed_ids: Arc<Mutex<BoundedIdSet>>,
}

impl NotificationEngine {
    /// Creates a new NotificationEngine bound to the given Tauri application handle and enabled state.
    pub fn new(app_handle: AppHandle, enabled: Arc<AtomicBool>) -> Self {
        let config_path = app_handle
            .path()
            .app_config_dir()
            .ok()
            .map(|dir| dir.join("notifications.json"));

        Self {
            app_handle,
            provider: Mutex::new(create_platform_provider()),
            storage: Arc::new(NotificationStorage::new_with_persistence(100, config_path)),
            enabled,
            captured_count: AtomicU64::new(0),
            glow_manager: std::sync::OnceLock::new(),
            settings_storage: std::sync::OnceLock::new(),
            dismissed_ids: Arc::new(Mutex::new(BoundedIdSet::new(1000))),
        }
    }

    /// Sets the active GlowManager for triggering visual screen-edge glow effects.
    pub fn set_glow_manager(&self, glow_manager: Arc<crate::glow::GlowManager>) {
        let _ = self.glow_manager.set(glow_manager);
    }

    /// Binds the centralized SettingsStorage instance.
    pub fn set_settings_storage(&self, storage: Arc<crate::settings::SettingsStorage>) {
        let _ = self.settings_storage.set(storage);
    }

    /// Dynamically synchronizes the storage history capacity with user settings.
    pub fn set_history_limit(&self, limit: usize) {
        self.storage.set_capacity(limit);
    }

    /// Whether notification monitoring is currently enabled.
    pub fn is_enabled(&self) -> bool {
        self.enabled.load(Ordering::SeqCst)
    }

    /// Total count of notifications successfully processed by the engine in this session.
    pub fn captured_count(&self) -> u64 {
        self.captured_count.load(Ordering::SeqCst)
    }

    /// Ingests a normalized notification and dispatches it through the event pipeline.
    ///
    /// Respects enabled status, dismissed ID suppression, duplicate checks,
    /// feed visibility preferences, and glow triggering.
    pub fn process_notification(&self, notification: Notification) -> Result<(), NotificationError> {
        let settings = self.settings_storage.get().map(|s| s.get());
        let is_active = self.is_enabled() && settings.as_ref().map(|s| s.enabled).unwrap_or(true);

        if !is_active {
            println!(
                "[NotificationEngine] Monitoring disabled; dropping notification ID='{}'",
                notification.id
            );
            return Err(NotificationError::Disabled);
        }

        // Drop notification if it was previously dismissed by the user
        let is_dismissed = {
            let dismissed = match self.dismissed_ids.lock() {
                Ok(guard) => guard,
                Err(poisoned) => poisoned.into_inner(),
            };
            dismissed.contains(&notification.id)
                || notification
                    .id
                    .strip_prefix("win-")
                    .map(|id| dismissed.contains(id))
                    .unwrap_or(false)
        };

        if is_dismissed {
            return Ok(());
        }

        // Prevent duplicate entries in the storage list
        let already_exists = self
            .storage
            .get_all()
            .iter()
            .any(|item| item.id == notification.id);

        if already_exists {
            return Ok(());
        }

        let show_notifications = settings.as_ref().map(|s| s.show_notifications).unwrap_or(true);
        if show_notifications {
            self.storage.add(notification.clone());
            self.captured_count.fetch_add(1, Ordering::SeqCst);

            self.app_handle
                .emit("notification-received", &notification)
                .map_err(|err| NotificationError::EmitFailed(err.to_string()))?;

            let _ = self.app_handle.emit("notification-created", &notification);
        } else {
            self.captured_count.fetch_add(1, Ordering::SeqCst);
        }

        // Trigger visual screen-edge glow overlay calibrated to notification urgency and duration
        if let Some(glow) = self.glow_manager.get() {
            glow.trigger_glow_for_notification(&notification);
        }

        Ok(())
    }

    /// Adds and processes a notification through the standard pipeline.
    pub fn add_notification(&self, notification: Notification) -> Result<Notification, NotificationError> {
        self.process_notification(notification.clone())?;
        Ok(notification)
    }

    /// Sends a synthetic test notification through the standard notification pipeline.
    pub fn send_test_notification(&self) -> Result<Notification, NotificationError> {
        let test_notification = Notification::new_test(
            "Curry",
            "Test Notification",
            "Notification pipeline is working.",
        );

        self.process_notification(test_notification.clone())?;
        Ok(test_notification)
    }

    /// Returns a list of all stored notifications (newest first).
    pub fn get_notifications(&self) -> Vec<Notification> {
        self.storage.get_all()
    }

    /// Removes a single notification by its unique ID and records it as dismissed.
    pub fn remove_notification(&self, id: &str) -> bool {
        if let Ok(mut set) = self.dismissed_ids.lock() {
            set.insert(id.to_string());
            if let Some(stripped) = id.strip_prefix("win-") {
                set.insert(stripped.to_string());
            }
        }
        self.storage.remove(id)
    }

    /// Returns whether a notification ID is currently recorded as dismissed.
    pub fn is_id_dismissed(&self, id: &str) -> bool {
        if let Ok(set) = self.dismissed_ids.lock() {
            set.contains(id) || id.strip_prefix("win-").map(|s| set.contains(s)).unwrap_or(false)
        } else {
            false
        }
    }

    /// Returns the number of IDs currently in the dismissed suppression set.
    pub fn dismissed_count(&self) -> usize {
        self.dismissed_ids.lock().map(|s| s.len()).unwrap_or(0)
    }

    /// Marks a notification as read by its unique ID.
    pub fn mark_as_read(&self, id: &str) -> bool {
        self.storage.mark_as_read(id)
    }

    /// Marks a notification as unread by its unique ID.
    pub fn mark_as_unread(&self, id: &str) -> bool {
        self.storage.mark_as_unread(id)
    }

    /// Sets the read status of a notification by its unique ID.
    pub fn set_read_status(&self, id: &str, read: bool) -> bool {
        self.storage.set_read_status(id, read)
    }

    /// Clears all stored notifications from memory and disk and suppresses them from re-delivery.
    pub fn clear_notifications(&self) {
        let existing = self.storage.get_all();
        if let Ok(mut set) = self.dismissed_ids.lock() {
            for item in existing {
                set.insert(item.id.clone());
                if let Some(stripped) = item.id.strip_prefix("win-") {
                    set.insert(stripped.to_string());
                }
            }
        }
        self.storage.clear();
    }

    /// Starts the underlying platform notification provider on its dedicated worker.
    pub fn start_listening(self: &Arc<Self>) -> Result<(), NotificationError> {
        let engine_clone = Arc::clone(self);
        let callback = Arc::new(move |notification: Notification| {
            let _ = engine_clone.process_notification(notification);
        });

        let mut provider = self
            .provider
            .lock()
            .map_err(|_| NotificationError::ProviderError("Failed to acquire provider lock".to_string()))?;

        provider.start(callback)
    }

    /// Stops the underlying platform notification provider.
    pub fn stop_listening(&self) -> Result<(), NotificationError> {
        let mut provider = self
            .provider
            .lock()
            .map_err(|_| NotificationError::ProviderError("Failed to acquire provider lock".to_string()))?;

        provider.stop()
    }

    /// Returns the comprehensive status of the notification pipeline.
    pub fn pipeline_status(&self) -> PipelineStatus {
        let (name, status) = match self.provider.lock() {
            Ok(p) => (p.name().to_string(), p.status()),
            Err(_) => (
                "Unknown".to_string(),
                ProviderStatus::Error("Lock failed".to_string()),
            ),
        };

        PipelineStatus {
            provider_name: name,
            provider_status: status,
            captured_count: self.captured_count.load(Ordering::SeqCst),
            is_enabled: self.is_enabled(),
        }
    }
}
