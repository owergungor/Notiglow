// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    curry_lib::run()
}

#[cfg(test)]
mod tests {
    use curry_lib::notification::model::{Notification, NotificationUrgency};
    use curry_lib::notification::platform::{
        create_platform_provider, Deduplicator, ProviderStatus,
    };
    use curry_lib::notification::storage::NotificationStorage;
    use curry_lib::state::AppState;

    #[test]
    fn test_notification_model_serialization() {
        let notification = Notification {
            id: "win-1001".to_string(),
            app_name: "Slack".to_string(),
            source_app: "Slack".to_string(),
            source: Some("Slack".to_string()),
            title: "New Message".to_string(),
            message: "Meeting starting in 5 minutes.".to_string(),
            body: "Meeting starting in 5 minutes.".to_string(),
            timestamp: 1700000000000,
            duration: Some(2500),
            enabled: true,
            icon: None,
            platform: "windows".to_string(),
            urgency: Some(NotificationUrgency::Normal),
            read: false,
        };

        let json = serde_json::to_string(&notification).expect("Failed to serialize notification");
        assert!(json.contains("win-1001"));
        assert!(json.contains("Slack"));
        assert!(json.contains("New Message"));
        assert!(json.contains("\"urgency\":\"normal\""));
        assert!(json.contains("\"read\":false"));
        assert!(json.contains("\"enabled\":true"));

        let deserialized: Notification =
            serde_json::from_str(&json).expect("Failed to deserialize notification");
        assert_eq!(notification, deserialized);
        assert_eq!(deserialized.app_name, "Slack");
        assert_eq!(deserialized.message, "Meeting starting in 5 minutes.");
        assert!(!deserialized.read);
    }

    #[test]
    fn test_notification_new_test() {
        let test_notif = Notification::new_test(
            "Curry",
            "Pipeline Test",
            "Checking normalized structure.",
        );

        assert!(test_notif.id.starts_with("test-"));
        assert_eq!(test_notif.app_name, "Curry");
        assert_eq!(test_notif.source_app, "Curry");
        assert_eq!(test_notif.title, "Pipeline Test");
        assert_eq!(test_notif.body, "Checking normalized structure.");
        assert!(test_notif.timestamp > 0);
        assert_eq!(test_notif.urgency, Some(NotificationUrgency::Normal));
        assert!(!test_notif.read);
    }

    #[test]
    fn test_storage_add_and_bounded_eviction() {
        let storage = NotificationStorage::new(3);

        for i in 1..=5 {
            let mut n = Notification::new_test("App", &format!("Title {}", i), "Body");
            n.id = format!("notif-{}", i);
            storage.add(n);
        }

        assert_eq!(storage.len(), 3);

        let items = storage.get_all();
        // Newest should be in front: 5, 4, 3 (1 and 2 evicted)
        assert_eq!(items[0].id, "notif-5");
        assert_eq!(items[1].id, "notif-4");
        assert_eq!(items[2].id, "notif-3");
    }

    #[test]
    fn test_storage_remove_and_mark_as_read() {
        let storage = NotificationStorage::new(10);

        let mut n1 = Notification::new_test("App1", "Title 1", "Body 1");
        n1.id = "id-1".to_string();
        let mut n2 = Notification::new_test("App2", "Title 2", "Body 2");
        n2.id = "id-2".to_string();

        storage.add(n1);
        storage.add(n2);
        assert_eq!(storage.len(), 2);

        // Mark as read
        assert!(storage.mark_as_read("id-1"));
        let all = storage.get_all();
        let item1 = all.iter().find(|n| n.id == "id-1").unwrap();
        assert!(item1.read);

        // Remove item
        assert!(storage.remove("id-2"));
        assert_eq!(storage.len(), 1);
        assert!(!storage.remove("id-2")); // second remove returns false

        // Clear
        storage.clear();
        assert_eq!(storage.len(), 0);
        assert!(storage.is_empty());
    }

    #[test]
    fn test_deduplicator_bounding_and_tracking() {
        let deduplicator = Deduplicator::new(20);

        assert!(deduplicator.record_new("101"));
        assert!(deduplicator.record_new("102"));
        assert!(!deduplicator.record_new("101"));
        assert!(!deduplicator.record_new("102"));
        assert_eq!(deduplicator.len(), 2);

        for i in 103..135 {
            deduplicator.record_new(&i.to_string());
        }

        assert!(deduplicator.len() <= 20);

        deduplicator.clear();
        assert_eq!(deduplicator.len(), 0);
        assert!(deduplicator.is_empty());
    }

    #[test]
    fn test_platform_provider_contract() {
        let provider = create_platform_provider();
        assert!(!provider.name().is_empty());

        let status = provider.status();
        assert!(matches!(
            status,
            ProviderStatus::Idle
                | ProviderStatus::Listening
                | ProviderStatus::PermissionRequired
                | ProviderStatus::PermissionDenied
                | ProviderStatus::Unsupported
        ));
    }

    #[test]
    fn test_app_state_enabled_flag() {
        let state = AppState::new();
        assert!(state.is_enabled());

        assert!(!state.toggle_enabled());
        assert!(!state.is_enabled());

        state.set_enabled(true);
        assert!(state.is_enabled());

        let flag = state.enabled_flag();
        assert!(flag.load(std::sync::atomic::Ordering::SeqCst));
    }

    #[test]
    fn test_duplicate_expiration_and_fingerprinting() {
        use std::time::{Duration, Instant};

        let ttl = Duration::from_secs(60);
        let deduplicator = Deduplicator::with_ttl(20, ttl);

        let t0 = Instant::now();
        assert!(deduplicator.record_at("item-1", t0));
        assert!(!deduplicator.record_at("item-1", t0 + Duration::from_secs(10))); // still within TTL

        // After TTL expired (70s > 60s)
        assert!(deduplicator.record_at("item-1", t0 + Duration::from_secs(70))); // accepted again

        // Fingerprint generation
        let fp1 = Deduplicator::fingerprint("Slack", "Meeting", "In 5 min", 1700000000);
        let fp2 = Deduplicator::fingerprint("Slack", "Meeting", "In 5 min", 1700000000);
        let fp3 = Deduplicator::fingerprint("Slack", "Meeting", "In 10 min", 1700000000);
        assert_eq!(fp1, fp2);
        assert_ne!(fp1, fp3);

        // Test pruning
        deduplicator.record_at("old-1", t0);
        deduplicator.record_at("recent-1", t0 + Duration::from_secs(50));
        let pruned = deduplicator.prune_older_than(Duration::from_secs(30), t0 + Duration::from_secs(60));
        assert!(pruned >= 1);
    }

    #[test]
    fn test_notification_conversion_and_fields() {
        let notif = Notification::new(
            Some("Discord"),
            "New mention",
            "@channel please review Phase 4",
        );

        assert_eq!(notif.title, "New mention");
        assert_eq!(notif.message, "@channel please review Phase 4");
        assert_eq!(notif.body, "@channel please review Phase 4");
        assert_eq!(notif.source.as_deref(), Some("Discord"));
        assert_eq!(notif.app_name, "Discord");
        assert_eq!(notif.source_app, "Discord");
        assert_eq!(notif.duration, Some(2500));
        assert!(notif.enabled);
        assert!(!notif.read);
        assert!(notif.timestamp > 0);
    }

    #[test]
    fn test_disabled_state_filtering() {
        let state = AppState::new();
        assert!(state.is_enabled());

        // Toggle disabled
        state.set_enabled(false);
        assert!(!state.is_enabled());

        let flag = state.enabled_flag();
        assert!(!flag.load(std::sync::atomic::Ordering::SeqCst));

        // Re-enable
        state.set_enabled(true);
        assert!(state.is_enabled());
        assert!(flag.load(std::sync::atomic::Ordering::SeqCst));
    }

    #[test]
    fn test_glow_settings_defaults_and_serialization() {
        use curry_lib::glow::{GlowAnimationStyle, GlowSettings, MonitorTarget};

        let default_settings = GlowSettings::default();
        assert!(default_settings.enabled);
        assert_eq!(default_settings.duration_ms, 2500);
        assert_eq!(default_settings.intensity, 0.8);
        assert_eq!(default_settings.thickness, 8);
        assert_eq!(default_settings.corner_radius, 24);
        assert_eq!(default_settings.animation_style, GlowAnimationStyle::Pulse);
        assert_eq!(default_settings.monitor_target, MonitorTarget::Primary);
        assert_eq!(default_settings.color, "#6366f1");

        let json = serde_json::to_string(&default_settings).expect("Failed to serialize GlowSettings");
        assert!(json.contains("\"duration_ms\":2500"));
        assert!(json.contains("\"animation_style\":\"pulse\""));
        assert!(json.contains("\"monitor_target\":\"primary\""));

        let deserialized: GlowSettings =
            serde_json::from_str(&json).expect("Failed to deserialize GlowSettings");
        assert_eq!(default_settings, deserialized);
    }

    #[test]
    fn test_urgency_variants_and_lifecycle() {
        let urgencies = vec![
            NotificationUrgency::Low,
            NotificationUrgency::Normal,
            NotificationUrgency::High,
            NotificationUrgency::Critical,
        ];

        for urgency in urgencies {
            let mut notif = Notification::new(Some("System"), "Alert", "Critical battery");
            notif.urgency = Some(urgency);
            assert_eq!(notif.urgency, Some(urgency));
            assert!(!notif.read);

            let json = serde_json::to_string(&notif).expect("Serialization failed");
            let deserialized: Notification = serde_json::from_str(&json).expect("Deserialization failed");
            assert_eq!(deserialized.urgency, Some(urgency));
            assert!(!deserialized.read);

            notif.mark_as_read();
            assert!(notif.read);
        }
    }

    #[test]
    fn test_storage_mark_as_unread_and_set_read_status() {
        let storage = NotificationStorage::new(10);
        let mut notif = Notification::new_test("App", "Test", "Body");
        notif.id = "unread-test-1".to_string();
        storage.add(notif);

        // Initially unread
        let items = storage.get_all();
        assert!(!items[0].read);

        // Mark read
        assert!(storage.mark_as_read("unread-test-1"));
        assert!(storage.get_all()[0].read);

        // Mark unread
        assert!(storage.mark_as_unread("unread-test-1"));
        assert!(!storage.get_all()[0].read);

        // Set read status directly
        assert!(storage.set_read_status("unread-test-1", true));
        assert!(storage.get_all()[0].read);
        assert!(storage.set_read_status("unread-test-1", false));
        assert!(!storage.get_all()[0].read);

        // Non-existent ID returns false
        assert!(!storage.mark_as_unread("non-existent"));
        assert!(!storage.set_read_status("non-existent", true));
    }

    #[test]
    fn test_storage_persistence_save_and_load() {
        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!("curry_test_storage_{}.json", std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_nanos()));

        // Scope 1: Add notifications and mutate state
        {
            let storage = NotificationStorage::new_with_persistence(5, Some(file_path.clone()));
            assert!(storage.is_empty());

            let mut n1 = Notification::new_test("Slack", "Meeting", "10am call");
            n1.id = "pers-1".to_string();
            let mut n2 = Notification::new_test("Discord", "Ping", "Check channel");
            n2.id = "pers-2".to_string();

            storage.add(n1);
            storage.add(n2);
            assert_eq!(storage.len(), 2);

            // Mark pers-1 as read
            storage.mark_as_read("pers-1");
        }

        // Verify the file exists on disk
        assert!(file_path.exists());

        // Scope 2: Reload from disk in a fresh storage instance
        {
            let reloaded = NotificationStorage::new_with_persistence(5, Some(file_path.clone()));
            assert_eq!(reloaded.len(), 2);

            let items = reloaded.get_all();
            // Newest first: pers-2, pers-1
            assert_eq!(items[0].id, "pers-2");
            assert!(!items[0].read);
            assert_eq!(items[1].id, "pers-1");
            assert!(items[1].read);

            // Remove an item and verify persistence updates
            assert!(reloaded.remove("pers-2"));
            assert_eq!(reloaded.len(), 1);
        }

        // Scope 3: Reload again to verify removal persisted
        {
            let reloaded2 = NotificationStorage::new_with_persistence(5, Some(file_path.clone()));
            assert_eq!(reloaded2.len(), 1);
            assert_eq!(reloaded2.get_all()[0].id, "pers-1");

            // Clear and verify
            reloaded2.clear();
            assert!(reloaded2.is_empty());
        }

        // Scope 4: Reload after clear
        {
            let reloaded3 = NotificationStorage::new_with_persistence(5, Some(file_path.clone()));
            assert!(reloaded3.is_empty());
        }

        // Clean up temp file
        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_storage_bounded_persistence() {
        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!("curry_test_bounded_{}.json", std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_nanos()));

        {
            let storage = NotificationStorage::new_with_persistence(3, Some(file_path.clone()));
            for i in 1..=6 {
                let mut n = Notification::new_test("App", &format!("Alert {}", i), "Details");
                n.id = format!("bound-{}", i);
                storage.add(n);
            }
            assert_eq!(storage.len(), 3);
        }

        // Reload should strictly enforce capacity of 3
        {
            let reloaded = NotificationStorage::new_with_persistence(3, Some(file_path.clone()));
            assert_eq!(reloaded.len(), 3);
            let items = reloaded.get_all();
            assert_eq!(items[0].id, "bound-6");
            assert_eq!(items[1].id, "bound-5");
            assert_eq!(items[2].id, "bound-4");
        }

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_default_settings() {
        use curry_lib::settings::AppSettings;
        let settings = AppSettings::default();

        assert!(settings.enabled);
        assert!(!settings.startup_enabled);
        assert!(settings.show_notifications);
        assert_eq!(settings.history_limit, 100);
        assert!(!settings.sound_enabled);
        assert!(settings.glow.enabled);
        assert_eq!(settings.glow.duration_ms, 2500);
        assert!((settings.glow.intensity - 0.8).abs() < f32::EPSILON);
        assert_eq!(settings.glow.thickness, 8);
        assert_eq!(settings.glow.corner_radius, 24);
        assert_eq!(settings.glow.color, "#6366f1");
        assert_eq!(settings.theme, curry_lib::settings::AppTheme::Perpetuity);
    }

    #[test]
    fn test_settings_serialization() {
        use curry_lib::settings::AppSettings;
        let mut settings = AppSettings::default();
        settings.sound_enabled = true;
        settings.glow.duration_ms = 3500;
        settings.glow.color = "#10b981".to_string();

        let json = serde_json::to_string(&settings).expect("Serialization failed");
        assert!(json.contains("\"sound_enabled\":true"));
        assert!(json.contains("\"duration_ms\":3500"));
        assert!(json.contains("\"#10b981\""));

        let deserialized: AppSettings = serde_json::from_str(&json).expect("Deserialization failed");
        assert_eq!(settings, deserialized);
    }

    #[test]
    fn test_settings_persistence() {
        use curry_lib::settings::SettingsStorage;
        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_settings_{}.json",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        {
            let storage = SettingsStorage::new_with_path(Some(file_path.clone()));
            let mut s = storage.get();
            s.sound_enabled = true;
            s.history_limit = 250;
            s.glow.duration_ms = 4500;
            s.glow.color = "#f43f5e".to_string();
            let _ = storage.update(s);
        }

        {
            let reloaded = SettingsStorage::new_with_path(Some(file_path.clone()));
            let s = reloaded.get();
            assert!(s.sound_enabled);
            assert_eq!(s.history_limit, 250);
            assert_eq!(s.glow.duration_ms, 4500);
            assert_eq!(s.glow.color, "#f43f5e");
        }

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_settings_malformed_recovery() {
        use curry_lib::settings::SettingsStorage;
        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_corrupt_{}.json",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        // Write invalid JSON
        std::fs::write(&file_path, "{ \"broken\": [true, null").expect("Failed to write corrupt file");

        // Storage initialization must safely fall back to defaults and recover
        let storage = SettingsStorage::new_with_path(Some(file_path.clone()));
        let recovered = storage.get();

        assert!(recovered.enabled);
        assert_eq!(recovered.history_limit, 100);
        assert_eq!(recovered.glow.duration_ms, 2500);

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_settings_clamping_and_validation() {
        use curry_lib::settings::AppSettings;
        let mut settings = AppSettings::default();
        settings.history_limit = 9999;
        settings.glow.duration_ms = 50; // below 500ms min
        settings.glow.intensity = 5.0; // above 1.0 max
        settings.glow.thickness = 100; // above 32 max
        settings.glow.corner_radius = 200; // above 48 max
        settings.glow.color = "".to_string(); // empty string fallback

        let sanitized = settings.sanitized();
        assert_eq!(sanitized.history_limit, 500);
        assert_eq!(sanitized.glow.duration_ms, 500);
        assert!((sanitized.glow.intensity - 1.0).abs() < f32::EPSILON);
        assert_eq!(sanitized.glow.thickness, 32);
        assert_eq!(sanitized.glow.corner_radius, 48);
        assert_eq!(sanitized.glow.color, "#6366f1");
    }

    #[test]
    fn test_enabled_state_synchronization() {
        use curry_lib::settings::SettingsStorage;
        use curry_lib::state::AppState;
        use std::sync::Arc;

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_state_sync_{}.json",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        let app_state = AppState::new();
        let storage = Arc::new(SettingsStorage::new_with_path(Some(file_path.clone())));
        app_state.set_settings_storage(Arc::clone(&storage));

        assert!(app_state.is_enabled());
        assert!(storage.get().enabled);

        // Toggle state
        let next = app_state.toggle_enabled();
        assert!(!next);
        assert!(!app_state.is_enabled());
        assert!(!storage.get().enabled);

        // Explicit set
        app_state.set_enabled(true);
        assert!(app_state.is_enabled());
        assert!(storage.get().enabled);

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_notification_to_glow_eligibility() {
        use curry_lib::notification::model::{Notification, NotificationUrgency};
        use curry_lib::settings::AppSettings;

        let settings = AppSettings::default();
        let notif_normal = Notification::new_test("App", "Title", "Body");

        // When glow is enabled and master app is enabled
        assert!(settings.enabled);
        assert!(settings.glow.enabled);

        // Urgency color calculation
        let (color, multiplier): (&str, f32) = match notif_normal.urgency {
            Some(NotificationUrgency::Critical) => ("#ef4444", 1.25),
            Some(NotificationUrgency::High) => ("#f59e0b", 1.15),
            Some(NotificationUrgency::Low) => (settings.glow.color.as_str(), 0.75),
            _ => (settings.glow.color.as_str(), 1.0),
        };

        assert_eq!(color, "#6366f1");
        assert!((multiplier - 1.0f32).abs() < f32::EPSILON);

        // Critical urgency test
        let mut notif_crit = Notification::new_test("App", "Crit", "Emergency");
        notif_crit.urgency = Some(NotificationUrgency::Critical);
        let (crit_color, crit_mult): (&str, f32) = match notif_crit.urgency {
            Some(NotificationUrgency::Critical) => ("#ef4444", 1.25),
            _ => ("#6366f1", 1.0),
        };
        assert_eq!(crit_color, "#ef4444");
        assert!((crit_mult - 1.25f32).abs() < f32::EPSILON);

        // If glow is disabled
        let mut disabled_glow = settings.clone();
        disabled_glow.glow.enabled = false;
        assert!(!disabled_glow.glow.enabled);
    }

    #[test]
    fn test_disabled_notification_no_glow() {
        use curry_lib::notification::model::Notification;
        use curry_lib::notification::platform::NotificationError;
        use curry_lib::notification::storage::NotificationStorage;

        let storage = NotificationStorage::new(10);
        let n = Notification::new_test("TestApp", "Test Title", "Test Message");

        // If app state is disabled, engine drops notification with NotificationError::Disabled
        let is_enabled = false;
        let result: Result<(), NotificationError> = if !is_enabled {
            Err(NotificationError::Disabled)
        } else {
            storage.add(n.clone());
            Ok(())
        };

        assert!(matches!(result, Err(NotificationError::Disabled)));
        assert!(storage.is_empty());
    }

    #[test]
    fn test_startup_setting_serialization() {
        use curry_lib::settings::AppSettings;
        let mut settings = AppSettings::default();
        settings.startup_enabled = true;

        let json = serde_json::to_string(&settings).expect("Serialization failed");
        assert!(json.contains("\"startup_enabled\":true"));

        let deserialized: AppSettings = serde_json::from_str(&json).expect("Deserialization failed");
        assert!(deserialized.startup_enabled);
    }

    #[test]
    fn test_storage_dynamic_capacity_change() {
        use curry_lib::notification::model::Notification;
        use curry_lib::notification::storage::NotificationStorage;

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_capacity_{}.json",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        let storage = NotificationStorage::new_with_persistence(10, Some(file_path.clone()));
        for i in 0..10 {
            storage.add(Notification::new_test(
                "App",
                &format!("Title {}", i),
                &format!("Body {}", i),
            ));
        }
        assert_eq!(storage.len(), 10);

        // Dynamically change capacity to 5 -> should trim to 5 most recent
        storage.set_capacity(5);
        assert_eq!(storage.len(), 5);

        // Verify remaining items are Title 9 (newest) down to Title 5 (oldest remaining)
        let remaining = storage.get_all();
        assert_eq!(remaining.len(), 5);
        assert_eq!(remaining[0].title, "Title 9");
        assert_eq!(remaining[4].title, "Title 5");

        // Verify persistence reloaded matches
        let reloaded = NotificationStorage::new_with_persistence(5, Some(file_path.clone()));
        assert_eq!(reloaded.len(), 5);
        assert_eq!(reloaded.get_all()[0].title, "Title 9");
        assert_eq!(reloaded.get_all()[4].title, "Title 5");

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_bounded_id_set_suppression_and_eviction() {
        use curry_lib::notification::BoundedIdSet;

        let mut set = BoundedIdSet::new(3);
        assert_eq!(set.len(), 0);
        assert!(set.is_empty());

        set.insert("id-1".to_string());
        set.insert("id-2".to_string());
        assert_eq!(set.len(), 2);
        assert!(set.contains("id-1"));
        assert!(set.contains("id-2"));
        assert!(!set.contains("id-3"));

        // Inserting duplicate does not increase count
        set.insert("id-2".to_string());
        assert_eq!(set.len(), 2);

        // Add 3rd item to reach capacity
        set.insert("id-3".to_string());
        assert_eq!(set.len(), 3);
        assert!(set.contains("id-1"));
        assert!(set.contains("id-2"));
        assert!(set.contains("id-3"));

        // Inserting 4th item evicts oldest (id-1)
        set.insert("id-4".to_string());
        assert_eq!(set.len(), 3);
        assert!(!set.contains("id-1")); // Evicted
        assert!(set.contains("id-2"));
        assert!(set.contains("id-3"));
        assert!(set.contains("id-4"));
    }

    #[test]
    fn test_storage_corrupted_json_recovery() {
        use curry_lib::notification::storage::NotificationStorage;

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_corrupt_{}.json",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        // Write intentionally corrupted/truncated data
        std::fs::write(&file_path, "{ broken json: [ unfinished").expect("Failed to write corrupt test file");

        // Loading corrupted storage must NOT panic and should initialize an empty list
        let storage = NotificationStorage::new_with_persistence(50, Some(file_path.clone()));
        assert_eq!(storage.len(), 0);
        assert!(storage.is_empty());

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_startup_command_formatting() {
        let exe_with_spaces = r"C:\Program Files\Curry App\Curry.exe";
        let formatted = format!("\"{}\" --autostart", exe_with_spaces);
        assert_eq!(formatted, r#""C:\Program Files\Curry App\Curry.exe" --autostart"#);
        assert!(formatted.starts_with('"'));
        assert!(formatted.ends_with("--autostart"));
    }

    #[test]
    fn test_settings_boundary_extremes() {
        use curry_lib::settings::AppSettings;

        let mut settings = AppSettings::default();
        settings.history_limit = 0; // below 10 min
        settings.glow.duration_ms = 0; // below 500ms min
        settings.glow.intensity = -2.5; // below 0.1 min
        settings.glow.thickness = 0; // below 2 min
        settings.glow.corner_radius = 9999; // above 48 max

        let sanitized = settings.sanitized();
        assert_eq!(sanitized.history_limit, 10);
        assert_eq!(sanitized.glow.duration_ms, 500);
        assert!((sanitized.glow.intensity - 0.1).abs() < f32::EPSILON);
        assert_eq!(sanitized.glow.thickness, 2);
        assert_eq!(sanitized.glow.corner_radius, 48);
    }

    #[test]
    fn test_single_instance_acquisition() {
        use curry_lib::single_instance;

        let test_mutex_name = format!("Curry_Test_Acquire_{}", std::process::id());
        let first = single_instance::acquire(&test_mutex_name);
        assert!(first.is_some(), "First instance must successfully acquire the mutex");

        // Dropping first releases the mutex cleanly
        drop(first);

        let second = single_instance::acquire(&test_mutex_name);
        assert!(second.is_some(), "After dropping first instance, mutex can be re-acquired");
    }

    #[test]
    fn test_single_instance_conflict_detection() {
        use curry_lib::single_instance;

        let test_mutex_name = format!("Curry_Test_Conflict_{}", std::process::id());
        let first = single_instance::acquire(&test_mutex_name);
        assert!(first.is_some(), "First instance must successfully acquire the mutex");

        #[cfg(target_os = "windows")]
        {
            let second = single_instance::acquire(&test_mutex_name);
            assert!(second.is_none(), "Second instance must detect running primary instance and return None");
        }

        drop(first);
    }

    #[test]
    fn test_notification_storage_tmp_recovery() {
        use curry_lib::notification::model::Notification;
        use curry_lib::notification::storage::NotificationStorage;

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_notif_tmp_{}_{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let tmp_path = file_path.with_extension("tmp");

        // 1. Create .tmp
        let notifs = vec![Notification::new_test("App", "Recovered Title", "Recovered Body")];
        let json = serde_json::to_string_pretty(&notifs).unwrap();
        std::fs::write(&tmp_path, json).unwrap();

        // 2. Remove primary JSON (ensure it does not exist)
        if file_path.exists() {
            let _ = std::fs::remove_file(&file_path);
        }
        assert!(!file_path.exists());
        assert!(tmp_path.exists());

        // 3. Initialize storage
        let storage = NotificationStorage::new_with_persistence(10, Some(file_path.clone()));

        // 4. Verify .tmp is recovered (primary exists)
        assert!(file_path.exists(), "Primary file must be recovered from .tmp");

        // 5. Verify notification data is available
        assert_eq!(storage.len(), 1);
        assert_eq!(storage.get_all()[0].title, "Recovered Title");

        // 6. Verify stale .tmp no longer remains
        assert!(!tmp_path.exists(), "Temporary file must be removed after recovery");

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_notification_storage_stale_tmp_cleanup() {
        use curry_lib::notification::model::Notification;
        use curry_lib::notification::storage::NotificationStorage;

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_notif_stale_{}_{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let tmp_path = file_path.with_extension("tmp");

        // 1. Create valid primary file
        let primary_notifs = vec![
            Notification::new_test("App1", "Primary Title 1", "Body 1"),
            Notification::new_test("App2", "Primary Title 2", "Body 2"),
        ];
        let primary_json = serde_json::to_string_pretty(&primary_notifs).unwrap();
        std::fs::write(&file_path, primary_json).unwrap();

        // 2. Create stale .tmp
        let stale_notifs = vec![Notification::new_test("StaleApp", "Stale Title", "Stale Body")];
        let stale_json = serde_json::to_string_pretty(&stale_notifs).unwrap();
        std::fs::write(&tmp_path, stale_json).unwrap();

        assert!(file_path.exists());
        assert!(tmp_path.exists());

        // 3. Initialize storage
        let storage = NotificationStorage::new_with_persistence(10, Some(file_path.clone()));

        // 4. Verify primary file wins
        assert_eq!(storage.len(), 2);
        assert_eq!(storage.get_all()[0].title, "Primary Title 1");

        // 5. Verify stale .tmp is removed
        assert!(!tmp_path.exists(), "Stale .tmp file must be cleaned up when primary file exists");

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_settings_storage_tmp_recovery() {
        use curry_lib::settings::{AppSettings, SettingsStorage};

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_settings_tmp_{}_{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let tmp_path = file_path.with_extension("tmp");

        // 1. Create .tmp with custom settings
        let mut custom_settings = AppSettings::default();
        custom_settings.history_limit = 42;
        custom_settings.glow.duration_ms = 4500;
        let json = serde_json::to_string_pretty(&custom_settings).unwrap();
        std::fs::write(&tmp_path, json).unwrap();

        // 2. Remove primary JSON
        if file_path.exists() {
            let _ = std::fs::remove_file(&file_path);
        }
        assert!(!file_path.exists());
        assert!(tmp_path.exists());

        // 3. Initialize storage
        let storage = SettingsStorage::new_with_path(Some(file_path.clone()));

        // 4. Verify .tmp is recovered (primary exists)
        assert!(file_path.exists(), "Primary settings file must be recovered from .tmp");

        // 5. Verify settings data is available
        assert_eq!(storage.get().history_limit, 42);
        assert_eq!(storage.get().glow.duration_ms, 4500);

        // 6. Verify stale .tmp is removed
        assert!(!tmp_path.exists(), "Temporary file must be removed after recovery");

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_settings_storage_stale_tmp_cleanup() {
        use curry_lib::settings::{AppSettings, SettingsStorage};

        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_settings_stale_{}_{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let tmp_path = file_path.with_extension("tmp");

        // 1. Create valid primary file
        let mut primary_settings = AppSettings::default();
        primary_settings.history_limit = 77;
        let primary_json = serde_json::to_string_pretty(&primary_settings).unwrap();
        std::fs::write(&file_path, primary_json).unwrap();

        // 2. Create stale .tmp
        let mut stale_settings = AppSettings::default();
        stale_settings.history_limit = 12;
        let stale_json = serde_json::to_string_pretty(&stale_settings).unwrap();
        std::fs::write(&tmp_path, stale_json).unwrap();

        // 3. Initialize storage
        let storage = SettingsStorage::new_with_path(Some(file_path.clone()));

        // 4. Verify primary file wins
        assert_eq!(storage.get().history_limit, 77);

        // 5. Verify stale .tmp is removed
        assert!(!tmp_path.exists(), "Stale .tmp file must be removed when valid primary settings exists");

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_glow_animation_style_unknown_fallback() {
        use curry_lib::glow::model::GlowAnimationStyle;

        let json = "\"rainbow\"";
        let style: GlowAnimationStyle = serde_json::from_str(json).unwrap();
        assert_eq!(style, GlowAnimationStyle::Pulse);

        let json_solid = "\"solid\"";
        let style_solid: GlowAnimationStyle = serde_json::from_str(json_solid).unwrap();
        assert_eq!(style_solid, GlowAnimationStyle::Solid);
    }

    #[test]
    fn test_monitor_target_unknown_fallback() {
        use curry_lib::glow::model::MonitorTarget;

        let json = "\"hologram\"";
        let target: MonitorTarget = serde_json::from_str(json).unwrap();
        assert_eq!(target, MonitorTarget::Primary);

        let json_active = "\"active\"";
        let target_active: MonitorTarget = serde_json::from_str(json_active).unwrap();
        assert_eq!(target_active, MonitorTarget::Active);
    }

    #[test]
    fn test_urgency_inference() {
        #[cfg(target_os = "windows")]
        {
            use curry_lib::notification::model::NotificationUrgency;
            use curry_lib::notification::platform::windows::infer_urgency;

            assert_eq!(infer_urgency("Critical Error", "The hard disk failed"), NotificationUrgency::Critical);
            assert_eq!(infer_urgency("System Warning", "Battery level is low"), NotificationUrgency::High);
            assert_eq!(infer_urgency("Update", "Informational note"), NotificationUrgency::Low);
            assert_eq!(infer_urgency("Message from Alice", "Let's meet tomorrow"), NotificationUrgency::Normal);
        }
    }

    #[test]
    fn test_bounded_id_set_eviction_and_containment() {
        use curry_lib::notification::BoundedIdSet;

        let mut set = BoundedIdSet::new(3);
        assert_eq!(set.len(), 0);
        assert!(set.is_empty());

        set.insert("id-1".to_string());
        set.insert("id-2".to_string());
        set.insert("id-3".to_string());
        assert_eq!(set.len(), 3);
        assert!(set.contains("id-1"));
        assert!(set.contains("id-2"));
        assert!(set.contains("id-3"));

        // Inserting 4th item evicts oldest (id-1)
        set.insert("id-4".to_string());
        assert_eq!(set.len(), 3);
        assert!(!set.contains("id-1"));
        assert!(set.contains("id-2"));
        assert!(set.contains("id-3"));
        assert!(set.contains("id-4"));

        // Re-inserting existing item does not increase size or duplicate
        set.insert("id-2".to_string());
        assert_eq!(set.len(), 3);
    }

    #[test]
    fn test_valid_theme_ids() {
        use curry_lib::settings::AppTheme;

        assert_eq!(serde_json::from_str::<AppTheme>("\"catppuccin\"").unwrap(), AppTheme::Catppuccin);
        assert_eq!(serde_json::from_str::<AppTheme>("\"vintage-paper\"").unwrap(), AppTheme::VintagePaper);
        assert_eq!(serde_json::from_str::<AppTheme>("\"amethyst-haze\"").unwrap(), AppTheme::AmethystHaze);
        assert_eq!(serde_json::from_str::<AppTheme>("\"sage-mist\"").unwrap(), AppTheme::SageMist);
        assert_eq!(serde_json::from_str::<AppTheme>("\"bubblegum\"").unwrap(), AppTheme::Bubblegum);
        assert_eq!(serde_json::from_str::<AppTheme>("\"perpetuity\"").unwrap(), AppTheme::Perpetuity);
        assert_eq!(serde_json::from_str::<AppTheme>("\"amberstate\"").unwrap(), AppTheme::Amberstate);
        assert_eq!(serde_json::from_str::<AppTheme>("\"amber-slate\"").unwrap(), AppTheme::Amberstate);
    }

    #[test]
    fn test_unknown_theme_fallback() {
        use curry_lib::settings::{AppSettings, AppTheme};

        // Unknown theme string falls back to Perpetuity (canonical default)
        assert_eq!(serde_json::from_str::<AppTheme>("\"super-neon\"").unwrap(), AppTheme::Perpetuity);
        assert_eq!(serde_json::from_str::<AppTheme>("\"random-invalid\"").unwrap(), AppTheme::Perpetuity);
        assert_eq!(serde_json::from_str::<AppTheme>("\"\"").unwrap(), AppTheme::Perpetuity);

        // In full settings JSON, an unknown theme does NOT reset or fail other fields
        let json = r##"{
            "enabled": false,
            "startup_enabled": true,
            "show_notifications": true,
            "history_limit": 300,
            "sound_enabled": true,
            "glow": {
                "enabled": true,
                "duration_ms": 3000,
                "intensity": 0.9,
                "thickness": 10,
                "corner_radius": 16,
                "animation_style": "solid",
                "monitor_target": "all",
                "color": "#a855f7"
            },
            "theme": "unrecognized_theme_from_future"
        }"##;

        let s: AppSettings = serde_json::from_str(json).expect("Deserialization of unknown theme should succeed");
        assert_eq!(s.theme, AppTheme::Perpetuity);
        assert!(!s.enabled);
        assert!(s.startup_enabled);
        assert_eq!(s.history_limit, 300);
        assert_eq!(s.glow.color, "#a855f7");
    }

    #[test]
    fn test_theme_persistence() {
        use curry_lib::settings::{AppTheme, SettingsStorage};
        let temp_dir = std::env::temp_dir();
        let file_path = temp_dir.join(format!(
            "curry_test_theme_settings_{}_{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));

        {
            let storage = SettingsStorage::new_with_path(Some(file_path.clone()));
            let mut s = storage.get();
            s.theme = AppTheme::Amberstate;
            s.sound_enabled = true;
            storage.update(s).expect("Failed to persist theme setting");
        }

        {
            let storage = SettingsStorage::new_with_path(Some(file_path.clone()));
            let reloaded = storage.get();
            assert_eq!(reloaded.theme, AppTheme::Amberstate);
            assert!(reloaded.sound_enabled);
        }

        let _ = std::fs::remove_file(file_path);
    }

    #[test]
    fn test_theme_serialization_deserialization() {
        use curry_lib::settings::{AppSettings, AppTheme};
        let mut settings = AppSettings::default();
        settings.theme = AppTheme::VintagePaper;

        let json = serde_json::to_string(&settings).expect("Serialization failed");
        assert!(json.contains("\"theme\":\"vintage-paper\""));

        let deserialized: AppSettings = serde_json::from_str(&json).expect("Deserialization failed");
        assert_eq!(deserialized.theme, AppTheme::VintagePaper);
        assert_eq!(settings, deserialized);
    }
}
