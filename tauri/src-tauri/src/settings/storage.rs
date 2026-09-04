use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;
use tauri::{AppHandle, Manager};

use crate::settings::model::AppSettings;
use crate::settings::startup::StartupManager;

/// Local JSON persistence and in-memory cache for AppSettings.
pub struct SettingsStorage {
    settings: Mutex<AppSettings>,
    config_path: Option<PathBuf>,
}

impl SettingsStorage {
    /// Initializes settings storage using the application's configuration directory.
    pub fn new(app: &AppHandle) -> Self {
        let config_path = app
            .path()
            .app_config_dir()
            .ok()
            .map(|dir| dir.join("settings.json"));

        Self::new_with_path(config_path)
    }

    /// Initializes settings storage with an explicit configuration file path (useful for testing).
    pub fn new_with_path(config_path: Option<PathBuf>) -> Self {
        let initial_settings = if let Some(ref path) = config_path {
            let tmp_path = path.with_extension("tmp");
            // If primary file is missing but a valid tmp file exists from an interrupted write, recover it
            if !path.exists() && tmp_path.exists() {
                let _ = fs::rename(&tmp_path, path);
            } else if path.exists() && tmp_path.exists() {
                // If primary file already exists, clean up any stale temporary file
                let _ = fs::remove_file(&tmp_path);
            }

            if path.exists() {
                match fs::read_to_string(path) {
                    Ok(data) => match serde_json::from_str::<AppSettings>(&data) {
                        Ok(loaded) => loaded.sanitized(),
                        Err(err) => {
                            eprintln!(
                                "[SettingsStorage] Warning: Failed to parse settings.json ({}); reverting to defaults.",
                                err
                            );
                            let def = AppSettings::default().sanitized();
                            if let Ok(json) = serde_json::to_string_pretty(&def) {
                                let _ = fs::write(path, json);
                            }
                            def
                        }
                    },
                    Err(err) => {
                        eprintln!(
                            "[SettingsStorage] Warning: Failed to read settings.json ({}); using defaults.",
                            err
                        );
                        AppSettings::default().sanitized()
                    }
                }
            } else {
                let def = AppSettings::default().sanitized();
                if let Some(parent) = path.parent() {
                    let _ = fs::create_dir_all(parent);
                }
                if let Ok(json) = serde_json::to_string_pretty(&def) {
                    let _ = fs::write(path, json);
                }
                def
            }
        } else {
            AppSettings::default().sanitized()
        };

        Self {
            settings: Mutex::new(initial_settings),
            config_path,
        }
    }

    /// Gets a cloned snapshot of current AppSettings.
    pub fn get(&self) -> AppSettings {
        match self.settings.lock() {
            Ok(guard) => guard.clone(),
            Err(poisoned) => poisoned.into_inner().clone(),
        }
    }

    pub fn update(&self, new_settings: AppSettings) -> Result<AppSettings, String> {
        let sanitized = new_settings.sanitized();
        let current = self.get();
        if current == sanitized {
            return Ok(sanitized);
        }

        // Apply startup configuration if changed
        if sanitized.startup_enabled != current.startup_enabled {
            if let Err(err) = StartupManager::set_enabled(sanitized.startup_enabled) {
                eprintln!("[SettingsStorage] Failed to update startup configuration: {}", err);
            }
        }

        // Persist to disk atomically
        if let Some(ref path) = self.config_path {
            if let Some(parent) = path.parent() {
                let _ = fs::create_dir_all(parent);
            }
            if let Ok(json) = serde_json::to_string_pretty(&sanitized) {
                let tmp_path = path.with_extension("tmp");
                if let Ok(mut file) = fs::File::create(&tmp_path) {
                    use std::io::Write;
                    if file.write_all(json.as_bytes()).is_ok() && file.flush().is_ok() {
                        let _ = file.sync_data();
                        drop(file);
                        if let Err(_) = fs::rename(&tmp_path, path) {
                            let _ = fs::remove_file(path);
                            let _ = fs::rename(&tmp_path, path);
                        }
                    }
                }
            }
        }

        if let Ok(mut guard) = self.settings.lock() {
            *guard = sanitized.clone();
        }

        Ok(sanitized)
    }
}
