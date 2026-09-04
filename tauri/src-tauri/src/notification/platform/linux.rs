use super::{NotificationCallback, NotificationError, NotificationProvider, ProviderStatus};

/// Linux Notification Provider placeholder.
pub struct LinuxNotificationProvider;

impl LinuxNotificationProvider {
    pub fn new() -> Self {
        Self
    }
}

impl NotificationProvider for LinuxNotificationProvider {
    fn name(&self) -> &'static str {
        "Linux Notification Provider"
    }

    fn is_supported(&self) -> bool {
        cfg!(target_os = "linux")
    }

    fn status(&self) -> ProviderStatus {
        if self.is_supported() {
            ProviderStatus::Idle
        } else {
            ProviderStatus::Unsupported
        }
    }

    fn start(&mut self, _callback: NotificationCallback) -> Result<(), NotificationError> {
        Err(NotificationError::NotImplemented(
            "Linux notification capture not implemented yet",
        ))
    }

    fn stop(&mut self) -> Result<(), NotificationError> {
        Ok(())
    }
}
