use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::thread::JoinHandle;

use super::{Deduplicator, NotificationCallback, NotificationError, NotificationProvider, ProviderStatus};
use crate::notification::model::{Notification, NotificationUrgency};

/// Native Windows Notification Provider using WinRT `UserNotificationListener`.
pub struct WindowsNotificationProvider {
    status: Arc<Mutex<ProviderStatus>>,
    is_running: Arc<AtomicBool>,
    worker_handle: Option<JoinHandle<()>>,
}

impl WindowsNotificationProvider {
    pub fn new() -> Self {
        Self {
            status: Arc::new(Mutex::new(ProviderStatus::Idle)),
            is_running: Arc::new(AtomicBool::new(false)),
            worker_handle: None,
        }
    }
}

impl NotificationProvider for WindowsNotificationProvider {
    fn name(&self) -> &'static str {
        "Windows Notification Provider"
    }

    fn is_supported(&self) -> bool {
        cfg!(target_os = "windows")
    }

    fn status(&self) -> ProviderStatus {
        match self.status.lock() {
            Ok(guard) => guard.clone(),
            Err(poisoned) => poisoned.into_inner().clone(),
        }
    }

    #[cfg(target_os = "windows")]
    fn start(&mut self, callback: NotificationCallback) -> Result<(), NotificationError> {
        if self.is_running.load(Ordering::SeqCst) {
            return Ok(());
        }

        self.is_running.store(true, Ordering::SeqCst);
        let is_running = Arc::clone(&self.is_running);
        let status = Arc::clone(&self.status);

        let handle = std::thread::Builder::new()
            .name("curry-win-listener".to_string())
            .spawn(move || {
                run_windows_listener(is_running, status, callback);
            })
            .map_err(|err| NotificationError::ProviderError(format!("Failed to spawn worker thread: {}", err)))?;

        self.worker_handle = Some(handle);
        Ok(())
    }

    #[cfg(not(target_os = "windows"))]
    fn start(&mut self, _callback: NotificationCallback) -> Result<(), NotificationError> {
        if let Ok(mut st) = self.status.lock() {
            *st = ProviderStatus::Unsupported;
        }
        Err(NotificationError::NotImplemented(
            "Windows notification provider is only supported on Windows",
        ))
    }

    fn stop(&mut self) -> Result<(), NotificationError> {
        self.is_running.store(false, Ordering::SeqCst);

        if let Some(handle) = self.worker_handle.take() {
            let _ = handle.join();
        }

        if let Ok(mut st) = self.status.lock() {
            if *st == ProviderStatus::Listening {
                *st = ProviderStatus::Idle;
            }
        }

        println!("[WindowsNotificationProvider] Listener stopped.");
        Ok(())
    }
}

#[cfg(target_os = "windows")]
fn run_windows_listener(
    is_running: Arc<AtomicBool>,
    status: Arc<Mutex<ProviderStatus>>,
    callback: NotificationCallback,
) {
    use windows::UI::Notifications::Management::{
        UserNotificationListener, UserNotificationListenerAccessStatus,
    };
    use windows::UI::Notifications::NotificationKinds;

    println!("[WindowsNotificationProvider] Starting Windows notification listener...");

    let listener = match UserNotificationListener::Current() {
        Ok(l) => l,
        Err(err) => {
            let err_msg = format!("Failed to acquire UserNotificationListener: {}", err);
            eprintln!("[WindowsNotificationProvider] {}", err_msg);
            if let Ok(mut st) = status.lock() {
                *st = ProviderStatus::Error(err_msg);
            }
            is_running.store(false, Ordering::SeqCst);
            return;
        }
    };

    let access_status = match listener.RequestAccessAsync() {
        Ok(async_op) => match async_op.join() {
            Ok(s) => s,
            Err(err) => {
                let err_msg = format!("RequestAccessAsync operation failed: {}", err);
                eprintln!("[WindowsNotificationProvider] {}", err_msg);
                if let Ok(mut st) = status.lock() {
                    *st = ProviderStatus::Error(err_msg);
                }
                is_running.store(false, Ordering::SeqCst);
                return;
            }
        },
        Err(err) => {
            let err_msg = format!("Failed to invoke RequestAccessAsync: {}", err);
            eprintln!("[WindowsNotificationProvider] {}", err_msg);
            if let Ok(mut st) = status.lock() {
                *st = ProviderStatus::Error(err_msg);
            }
            is_running.store(false, Ordering::SeqCst);
            return;
        }
    };

    match access_status {
        UserNotificationListenerAccessStatus::Allowed => {
            println!("[WindowsNotificationProvider] UserNotificationListener access granted.");
            if let Ok(mut st) = status.lock() {
                *st = ProviderStatus::Listening;
            }
        }
        UserNotificationListenerAccessStatus::Denied => {
            eprintln!("[WindowsNotificationProvider] UserNotificationListener access was denied by user.");
            if let Ok(mut st) = status.lock() {
                *st = ProviderStatus::PermissionDenied;
            }
            is_running.store(false, Ordering::SeqCst);
            return;
        }
        _ => {
            eprintln!("[WindowsNotificationProvider] UserNotificationListener access is unspecified (permission required).");
            if let Ok(mut st) = status.lock() {
                *st = ProviderStatus::PermissionRequired;
            }
            is_running.store(false, Ordering::SeqCst);
            return;
        }
    }

    let deduplicator = Deduplicator::new(500);

    // Initial snapshot: Seed deduplicator with existing notifications in Action Center
    // to prevent historical notifications from generating alert bursts on startup.
    if let Ok(async_op) = listener.GetNotificationsAsync(NotificationKinds::Toast) {
        if let Ok(existing) = async_op.join() {
            let count = existing.Size().unwrap_or(0);
            for i in 0..count {
                if let Ok(notif) = existing.GetAt(i) {
                    if let Ok(id) = notif.Id() {
                        deduplicator.record_new(&id.to_string());
                    }
                }
            }
            println!(
                "[WindowsNotificationProvider] Initial snapshot: {} existing notifications recorded in deduplicator.",
                count
            );
        }
    }

    println!("[WindowsNotificationProvider] Actively listening for new Windows toast notifications (polling interval 250ms).");

    while is_running.load(Ordering::SeqCst) {
        if let Ok(async_op) = listener.GetNotificationsAsync(NotificationKinds::Toast) {
            if let Ok(notifications) = async_op.join() {
                let count = notifications.Size().unwrap_or(0);
                for i in 0..count {
                    if let Ok(notif) = notifications.GetAt(i) {
                        let id = notif.Id().unwrap_or(0);
                        let id_str = if id > 0 {
                            id.to_string()
                        } else {
                            let ts = notif.CreationTime().map(|dt| dt.UniversalTime).unwrap_or(0);
                            format!("fp-{}", ts)
                        };

                        if !deduplicator.record_new(&id_str) {
                            // Duplicate suppressed
                            continue;
                        }

                        if let Some(normalized) = extract_notification(&notif, id) {
                            println!(
                                "[WindowsNotificationProvider] Notification received: ID={}",
                                id
                            );
                            callback(normalized);
                        }
                    }
                }
            }
        }

        // Sleep with fine-grained 10ms increments to stay promptly responsive to stop requests
        for _ in 0..25 {
            if !is_running.load(Ordering::SeqCst) {
                break;
            }
            std::thread::sleep(std::time::Duration::from_millis(10));
        }
    }

    println!("[WindowsNotificationProvider] Polling loop finished.");
}

#[cfg(target_os = "windows")]
fn extract_notification(
    notif: &windows::UI::Notifications::UserNotification,
    id: u32,
) -> Option<Notification> {
    use windows::core::HSTRING;

    let mut app_name = "Windows App".to_string();
    if let Ok(app_info) = notif.AppInfo() {
        if let Ok(disp) = app_info.DisplayInfo() {
            if let Ok(name) = disp.DisplayName() {
                let name_str = name.to_string();
                if !name_str.trim().is_empty() {
                    app_name = name_str;
                }
            }
        }

        if app_name == "Windows App" {
            if let Ok(aumid) = app_info.AppUserModelId() {
                let aumid_str = aumid.to_string();
                if !aumid_str.trim().is_empty() {
                    app_name = aumid_str;
                }
            }
        }
    }

    let mut title = String::new();
    let mut body = String::new();

    if let Ok(toast) = notif.Notification() {
        if let Ok(visual) = toast.Visual() {
            let toast_generic = HSTRING::from("ToastGeneric");
            if let Ok(binding) = visual.GetBinding(&toast_generic) {
                if let Ok(text_elements) = binding.GetTextElements() {
                    let text_count = text_elements.Size().unwrap_or(0);
                    if text_count > 0 {
                        if let Ok(t0) = text_elements.GetAt(0) {
                            if let Ok(text) = t0.Text() {
                                title = text.to_string();
                            }
                        }
                    }
                    if text_count > 1 {
                        if let Ok(t1) = text_elements.GetAt(1) {
                            if let Ok(text) = t1.Text() {
                                body = text.to_string();
                            }
                        }
                    }
                    if text_count > 2 {
                        for idx in 2..text_count {
                            if let Ok(elem) = text_elements.GetAt(idx) {
                                if let Ok(text) = elem.Text() {
                                    let s = text.to_string();
                                    if !s.trim().is_empty() {
                                        if !body.is_empty() {
                                            body.push('\n');
                                        }
                                        body.push_str(&s);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if title.trim().is_empty() && !body.trim().is_empty() {
        title = format!("{} Notification", app_name);
    } else if !title.trim().is_empty() && body.trim().is_empty() {
        body = title.clone();
    } else if title.trim().is_empty() && body.trim().is_empty() {
        title = format!("{} Notification", app_name);
        body = "New desktop alert received.".to_string();
    }

    let urgency = Some(infer_urgency(&title, &body));

    let timestamp = notif
        .CreationTime()
        .map(|dt| {
            let filetime = dt.UniversalTime;
            if filetime > 116_444_736_000_000_000 {
                (filetime - 116_444_736_000_000_000) / 10_000
            } else {
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_millis() as i64
            }
        })
        .unwrap_or_else(|_| {
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as i64
        });

    Some(Notification {
        id: format!("win-{}", id),
        title,
        message: body.clone(),
        body,
        timestamp,
        duration: Some(2500),
        enabled: true,
        source: Some(app_name.clone()),
        app_name: app_name.clone(),
        source_app: app_name,
        icon: None,
        platform: "windows".to_string(),
        urgency,
        read: false,
    })
}

/// Infers notification urgency level using privacy-safe keyword classification.
pub fn infer_urgency(title: &str, body: &str) -> NotificationUrgency {
    let lower_title = title.to_lowercase();
    let lower_body = body.to_lowercase();
    let combined = format!("{} {}", lower_title, lower_body);

    if combined.contains("critical")
        || combined.contains("severe")
        || combined.contains("fatal")
        || combined.contains("emergency")
    {
        NotificationUrgency::Critical
    } else if combined.contains("warning")
        || combined.contains("urgent")
        || combined.contains("alert")
        || combined.contains("high priority")
    {
        NotificationUrgency::High
    } else if combined.contains("low priority")
        || combined.contains("minor")
        || combined.contains("informational")
    {
        NotificationUrgency::Low
    } else {
        NotificationUrgency::Normal
    }
}
