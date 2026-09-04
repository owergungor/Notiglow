use serde::{Deserialize, Serialize};

fn default_true() -> bool {
    true
}

/// Notification priority / urgency level.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum NotificationUrgency {
    Low,
    Normal,
    High,
    Critical,
}

/// Normalized, cross-platform notification model for Curry.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Notification {
    /// Unique identifier for this notification instance.
    pub id: String,
    /// Title / header of the notification.
    pub title: String,
    /// Message body / content of the notification.
    pub message: String,
    /// Synchronized body field for backward compatibility.
    pub body: String,
    /// Timestamp in milliseconds since Unix epoch.
    pub timestamp: i64,
    /// Display duration in milliseconds (e.g. 2500ms).
    #[serde(default)]
    pub duration: Option<u64>,
    /// Whether this notification is active/enabled.
    #[serde(default = "default_true")]
    pub enabled: bool,
    /// Source application name (e.g. "Slack", "WhatsApp", "Discord").
    #[serde(default)]
    pub source: Option<String>,
    /// Originating application display name (synchronized with source).
    #[serde(default)]
    pub app_name: String,
    /// Originating application identifier (synchronized with source).
    #[serde(default)]
    pub source_app: String,
    /// Optional icon path or data URI.
    pub icon: Option<String>,
    /// Platform from which the notification originated ("windows", "macos", "linux", "test").
    #[serde(default)]
    pub platform: String,
    /// Optional urgency classification.
    pub urgency: Option<NotificationUrgency>,
    /// Whether the user has marked this notification as read.
    #[serde(default)]
    pub read: bool,
}

impl Notification {
    /// Creates a new notification with default duration and enabled state.
    pub fn new(source: Option<&str>, title: &str, message: &str) -> Self {
        let now_millis = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default()
            .as_millis() as i64;

        let app_name = source.unwrap_or("Curry").to_string();

        Self {
            id: format!("notif-{}", now_millis),
            title: title.to_string(),
            message: message.to_string(),
            body: message.to_string(),
            timestamp: now_millis,
            duration: Some(2500),
            enabled: true,
            source: Some(app_name.clone()),
            app_name: app_name.clone(),
            source_app: app_name,
            icon: None,
            platform: std::env::consts::OS.to_string(),
            urgency: Some(NotificationUrgency::Normal),
            read: false,
        }
    }

    /// Creates a test notification.
    pub fn new_test(app_name: &str, title: &str, body: &str) -> Self {
        let mut notif = Self::new(Some(app_name), title, body);
        notif.id = format!("test-{}", notif.timestamp);
        notif
    }

    /// Marks the notification as read.
    pub fn mark_as_read(&mut self) {
        self.read = true;
    }
}
