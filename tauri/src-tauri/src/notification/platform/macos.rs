use super::{NotificationCallback, NotificationError, NotificationProvider, ProviderStatus};

/// macOS Notification Provider placeholder.
pub struct MacOSNotificationProvider;

impl MacOSNotificationProvider {
    pub fn new() -> Self {
        Self
    }
}

impl NotificationProvider for MacOSNotificationProvider {
    fn name(&self) -> &'static str {
        "macOS Notification Provider"
    }

    fn is_supported(&self) -> bool {
        cfg!(target_os = "macos")
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
            "macOS notification capture not implemented yet",
        ))
    }

    fn stop(&mut self) -> Result<(), NotificationError> {
        Ok(())
    }
}
