use std::collections::VecDeque;
use std::fs;
use std::io::Write;
use std::path::PathBuf;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Mutex;

use crate::notification::model::Notification;

/// Thread-safe, bounded storage for recently captured notifications with atomic local JSON persistence.
///
/// Ensures memory usage remains strictly bounded while providing efficient
/// access for frontend history queries, item removal, read-status tracking,
/// dynamic capacity adjustment, and restart persistence without transmitting any data over the network.
pub struct NotificationStorage {
    notifications: Mutex<VecDeque<Notification>>,
    max_capacity: AtomicUsize,
    config_path: Option<PathBuf>,
}

impl Default for NotificationStorage {
    fn default() -> Self {
        Self::new(100)
    }
}

impl NotificationStorage {
    /// Creates a new in-memory `NotificationStorage` with the specified maximum capacity.
    pub fn new(max_capacity: usize) -> Self {
        Self::new_with_persistence(max_capacity, None)
    }

    /// Creates a new `NotificationStorage` with the specified maximum capacity and optional local disk persistence.
    pub fn new_with_persistence(max_capacity: usize, config_path: Option<PathBuf>) -> Self {
        let capacity = max_capacity.max(1);

        let initial_items = if let Some(ref path) = config_path {
            let tmp_path = path.with_extension("tmp");
            // If primary file is absent but a valid tmp file exists from an interrupted write, recover it
            if !path.exists() && tmp_path.exists() {
                let _ = fs::rename(&tmp_path, path);
            } else if path.exists() && tmp_path.exists() {
                // If primary file already exists, clean up any stale temporary file
                let _ = fs::remove_file(&tmp_path);
            }

            if path.exists() {
                match fs::read_to_string(path) {
                    Ok(data) => match serde_json::from_str::<Vec<Notification>>(&data) {
                        Ok(items) => {
                            let mut deque = VecDeque::with_capacity(capacity);
                            for item in items.into_iter().take(capacity) {
                                deque.push_back(item);
                            }
                            deque
                        }
                        Err(err) => {
                            eprintln!("[NotificationStorage] Corrupt notifications.json detected ({}). Initializing clean storage.", err);
                            VecDeque::new()
                        }
                    },
                    Err(err) => {
                        eprintln!("[NotificationStorage] Failed to read notifications.json: {}", err);
                        VecDeque::new()
                    }
                }
            } else {
                VecDeque::new()
            }
        } else {
            VecDeque::new()
        };

        Self {
            notifications: Mutex::new(initial_items),
            max_capacity: AtomicUsize::new(capacity),
            config_path,
        }
    }

    /// Helper to persist the current bounded list to disk atomically via a temporary file with flush & sync.
    fn persist(&self, list: &VecDeque<Notification>) {
        if let Some(ref path) = self.config_path {
            if let Some(parent) = path.parent() {
                let _ = fs::create_dir_all(parent);
            }
            let items: Vec<&Notification> = list.iter().collect();
            if let Ok(json) = serde_json::to_string_pretty(&items) {
                let tmp_path = path.with_extension("tmp");
                if let Ok(mut file) = fs::File::create(&tmp_path) {
                    if file.write_all(json.as_bytes()).is_ok() && file.flush().is_ok() {
                        let _ = file.sync_data();
                        drop(file);
                        // Atomic replacement: rename tmp over target file
                        if let Err(_) = fs::rename(&tmp_path, path) {
                            // On Windows, if destination exists, rename may fail; fallback to replace
                            let _ = fs::remove_file(path);
                            let _ = fs::rename(&tmp_path, path);
                        }
                    }
                }
            }
        }
    }

    /// Dynamically updates the maximum capacity, immediately trimming excess older items and persisting.
    pub fn set_capacity(&self, new_capacity: usize) {
        let cap = new_capacity.max(1);
        self.max_capacity.store(cap, Ordering::SeqCst);

        let mut list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        let mut trimmed = false;
        while list.len() > cap {
            list.pop_back();
            trimmed = true;
        }

        if trimmed {
            self.persist(&list);
        }
    }

    /// Returns the currently configured maximum capacity.
    pub fn capacity(&self) -> usize {
        self.max_capacity.load(Ordering::SeqCst)
    }

    /// Appends a new notification to the front of the collection (newest first).
    /// If capacity is exceeded, the oldest notification is dropped from the back.
    pub fn add(&self, notification: Notification) {
        let mut list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        list.push_front(notification);

        let cap = self.max_capacity.load(Ordering::SeqCst);
        while list.len() > cap {
            list.pop_back();
        }

        self.persist(&list);
    }

    /// Returns a snapshot of all currently stored notifications (newest first).
    pub fn get_all(&self) -> Vec<Notification> {
        let list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        list.iter().cloned().collect()
    }

    /// Removes a notification by its unique ID. Returns `true` if an item was removed.
    pub fn remove(&self, id: &str) -> bool {
        let mut list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        if let Some(pos) = list.iter().position(|n| n.id == id) {
            list.remove(pos);
            self.persist(&list);
            true
        } else {
            false
        }
    }

    /// Marks a notification as read by its unique ID. Returns `true` if updated.
    pub fn mark_as_read(&self, id: &str) -> bool {
        self.set_read_status(id, true)
    }

    /// Marks a notification as unread by its unique ID. Returns `true` if updated.
    pub fn mark_as_unread(&self, id: &str) -> bool {
        self.set_read_status(id, false)
    }

    /// Sets the read status of a notification by its unique ID. Returns `true` if updated.
    pub fn set_read_status(&self, id: &str, read: bool) -> bool {
        let mut list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        if let Some(item) = list.iter_mut().find(|n| n.id == id) {
            item.read = read;
            self.persist(&list);
            true
        } else {
            false
        }
    }

    /// Clears all stored notifications from memory and disk.
    pub fn clear(&self) {
        let mut list = match self.notifications.lock() {
            Ok(guard) => guard,
            Err(poisoned) => poisoned.into_inner(),
        };

        list.clear();
        self.persist(&list);
    }

    /// Returns the current count of stored notifications.
    pub fn len(&self) -> usize {
        match self.notifications.lock() {
            Ok(guard) => guard.len(),
            Err(poisoned) => poisoned.into_inner().len(),
        }
    }

    /// Returns whether the storage is empty.
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    /// Returns the configured config path, if any.
    pub fn config_path(&self) -> Option<&PathBuf> {
        self.config_path.as_ref()
    }
}
