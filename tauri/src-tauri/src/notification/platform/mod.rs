pub mod linux;
pub mod macos;
pub mod windows;

use std::fmt;
use std::sync::{Arc, Mutex};
use serde::{Deserialize, Serialize};

use crate::notification::model::Notification;

/// Callback type for dispatching detected notifications from platform listeners to the engine.
pub type NotificationCallback = Arc<dyn Fn(Notification) + Send + Sync>;

/// Runtime status of a notification provider.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ProviderStatus {
    /// Provider is idle and not capturing notifications.
    Idle,
    /// Provider is actively capturing OS notifications.
    Listening,
    /// Windows/OS requires the user to grant notification listener permission.
    PermissionRequired,
    /// Notification listener permission was explicitly denied by the user.
    PermissionDenied,
    /// Notification capture is unsupported on this operating system or version.
    Unsupported,
    /// Provider encountered an unrecoverable runtime error.
    Error(String),
}

/// Errors that can occur within the notification engine or providers.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum NotificationError {
    NotImplemented(&'static str),
    ProviderError(String),
    Disabled,
    EmitFailed(String),
}

impl fmt::Display for NotificationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::NotImplemented(msg) => write!(f, "Feature not implemented: {}", msg),
            Self::ProviderError(msg) => write!(f, "Notification provider error: {}", msg),
            Self::Disabled => write!(f, "Notification monitoring is currently disabled"),
            Self::EmitFailed(msg) => write!(f, "Failed to emit notification event: {}", msg),
        }
    }
}

impl std::error::Error for NotificationError {}

/// Cross-platform abstraction for OS notification capture providers.
pub trait NotificationProvider: Send + Sync {
    /// Friendly display name of this provider.
    fn name(&self) -> &'static str;

    /// Whether this provider is supported on the current runtime OS.
    fn is_supported(&self) -> bool;

    /// Current operational status of the provider.
    fn status(&self) -> ProviderStatus;

    /// Starts capturing notifications and dispatches them to the supplied callback.
    fn start(&mut self, callback: NotificationCallback) -> Result<(), NotificationError>;

    /// Stops capturing notifications and releases any platform hooks or listeners.
    fn stop(&mut self) -> Result<(), NotificationError>;
}

/// Thread-safe deduplicator tracking recently processed notification identifiers with expiration.
///
/// Automatically prunes expired entries and enforces a bounded capacity threshold
/// to maintain a strictly bounded memory footprint.
#[derive(Debug)]
pub struct Deduplicator {
    seen: Mutex<std::collections::HashMap<String, std::time::Instant>>,
    max_capacity: usize,
    ttl: std::time::Duration,
}

impl Default for Deduplicator {
    fn default() -> Self {
        Self::new(500)
    }
}

impl Deduplicator {
    pub fn new(max_capacity: usize) -> Self {
        Self::with_ttl(max_capacity, std::time::Duration::from_secs(15 * 60)) // 15 minutes TTL
    }

    pub fn with_ttl(max_capacity: usize, ttl: std::time::Duration) -> Self {
        Self {
            seen: Mutex::new(std::collections::HashMap::new()),
            max_capacity: max_capacity.max(10),
            ttl,
        }
    }

    /// Computes a stable fingerprint string from notification content when an ID is absent.
    pub fn fingerprint(source: &str, title: &str, body: &str, timestamp: i64) -> String {
        format!("{}:{}:{}:{}", source.trim(), title.trim(), body.trim(), timestamp)
    }

    /// Records an identifier. Returns `true` if newly recorded, `false` if it was already present and unexpired.
    pub fn record_new(&self, id: &str) -> bool {
        self.record_at(id, std::time::Instant::now())
    }

    /// Records an identifier at a specific instant (useful for testing expiration).
    pub fn record_at(&self, id: &str, now: std::time::Instant) -> bool {
        let mut map = match self.seen.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        // Prune expired entries
        map.retain(|_, time| now.duration_since(*time) < self.ttl);

        if let Some(recorded_time) = map.get(id) {
            if now.duration_since(*recorded_time) < self.ttl {
                return false;
            }
        }

        if map.len() >= self.max_capacity {
            // Remove oldest entries to keep within capacity
            let mut entries: Vec<(String, std::time::Instant)> = map.drain().collect();
            entries.sort_by_key(|(_, t)| *t);
            let to_retain = entries.split_off(self.max_capacity / 2);
            for (k, v) in to_retain {
                map.insert(k, v);
            }
        }

        map.insert(id.to_string(), now);
        true
    }

    /// Checks whether an identifier is considered a duplicate without recording it.
    pub fn is_duplicate(&self, id: &str) -> bool {
        let map = match self.seen.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        let now = std::time::Instant::now();
        if let Some(recorded_time) = map.get(id) {
            now.duration_since(*recorded_time) < self.ttl
        } else {
            false
        }
    }

    /// Prunes entries older than the specified duration relative to the given reference instant.
    pub fn prune_older_than(&self, cutoff: std::time::Duration, relative_to: std::time::Instant) -> usize {
        let mut map = match self.seen.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        let initial_len = map.len();
        map.retain(|_, time| relative_to.duration_since(*time) < cutoff);
        initial_len - map.len()
    }

    pub fn len(&self) -> usize {
        match self.seen.lock() {
            Ok(guard) => guard.len(),
            Err(poisoned) => poisoned.into_inner().len(),
        }
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    pub fn clear(&self) {
        if let Ok(mut guard) = self.seen.lock() {
            guard.clear();
        }
    }
}

/// Factory function selecting the appropriate NotificationProvider at compile-time.
pub fn create_platform_provider() -> Box<dyn NotificationProvider> {
    #[cfg(target_os = "windows")]
    {
        Box::new(windows::WindowsNotificationProvider::new())
    }

    #[cfg(target_os = "macos")]
    {
        Box::new(macos::MacOSNotificationProvider::new())
    }

    #[cfg(target_os = "linux")]
    {
        Box::new(linux::LinuxNotificationProvider::new())
    }

    #[cfg(not(any(target_os = "windows", target_os = "macos", target_os = "linux")))]
    {
        struct UnsupportedProvider;
        impl NotificationProvider for UnsupportedProvider {
            fn name(&self) -> &'static str {
                "Unsupported Platform"
            }
            fn is_supported(&self) -> bool {
                false
            }
            fn status(&self) -> ProviderStatus {
                ProviderStatus::Unsupported
            }
            fn start(&mut self, _callback: NotificationCallback) -> Result<(), NotificationError> {
                Err(NotificationError::NotImplemented(
                    "Current platform is not supported for notification monitoring",
                ))
            }
            fn stop(&mut self) -> Result<(), NotificationError> {
                Ok(())
            }
        }
        Box::new(UnsupportedProvider)
    }
}
