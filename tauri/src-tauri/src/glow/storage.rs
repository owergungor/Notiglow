use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;
use tauri::{AppHandle, Manager};

use crate::glow::model::GlowSettings;

/// Local JSON persistence and in-memory cache for GlowSettings.
pub struct GlowStorage {
    settings: Mutex<GlowSettings>,
    config_path: Option<PathBuf>,
}

impl GlowStorage {
    /// Initializes storage by loading existing configuration from disk or using defaults.
    pub fn new(app: &AppHandle) -> Self {
        let config_path = app
            .path()
            .app_config_dir()
            .ok()
            .map(|dir| dir.join("glow_settings.json"));

        let initial_settings = if let Some(ref path) = config_path {
            if path.exists() {
                match fs::read_to_string(path) {
                    Ok(data) => serde_json::from_str::<GlowSettings>(&data).unwrap_or_default(),
                    Err(_) => GlowSettings::default(),
                }
            } else {
                GlowSettings::default()
            }
        } else {
            GlowSettings::default()
        };

        Self {
            settings: Mutex::new(initial_settings),
            config_path,
        }
    }

    /// Gets a cloned snapshot of the current GlowSettings.
    pub fn get(&self) -> GlowSettings {
        match self.settings.lock() {
            Ok(guard) => guard.clone(),
            Err(poisoned) => poisoned.into_inner().clone(),
        }
    }

    /// Updates the in-memory settings and persists them to disk.
    pub fn update(&self, new_settings: GlowSettings) -> Result<(), String> {
        if let Some(ref path) = self.config_path {
            if let Some(parent) = path.parent() {
                let _ = fs::create_dir_all(parent);
            }
            if let Ok(json) = serde_json::to_string_pretty(&new_settings) {
                let _ = fs::write(path, json);
            }
        }

        if let Ok(mut guard) = self.settings.lock() {
            *guard = new_settings;
        }

        Ok(())
    }
}
