use std::sync::OnceLock;
use tauri::{
    menu::{CheckMenuItem, Menu, MenuItem, PredefinedMenuItem},
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    AppHandle, Emitter, Manager,
};

use crate::state::AppState;

static TRAY_TOGGLE_ITEM: OnceLock<CheckMenuItem<tauri::Wry>> = OnceLock::new();

/// Updates the checkmark state of the tray menu toggle item.
pub fn set_tray_enabled_state(enabled: bool) {
    if let Some(item) = TRAY_TOGGLE_ITEM.get() {
        let _ = item.set_checked(enabled);
    }
}

/// Restores, unminimizes, and focuses the main application window.
pub fn restore_main_window(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

/// Sets up the cross-platform system tray icon and native context menu.
pub fn setup_tray(app: &AppHandle) -> Result<(), Box<dyn std::error::Error>> {
    let state = app.state::<AppState>();
    let initial_enabled = state.is_enabled();

    let header_item = MenuItem::with_id(app, "header", "Curry", false, None::<&str>)?;
    let sep1 = PredefinedMenuItem::separator(app)?;
    let open_item = MenuItem::with_id(app, "open", "Open Curry", true, None::<&str>)?;
    let toggle_item = CheckMenuItem::with_id(
        app,
        "toggle_enabled",
        "Enabled",
        true,
        initial_enabled,
        None::<&str>,
    )?;
    let sep2 = PredefinedMenuItem::separator(app)?;
    let settings_item = MenuItem::with_id(app, "settings", "Settings", true, None::<&str>)?;
    let sep3 = PredefinedMenuItem::separator(app)?;
    let quit_item = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;

    let menu = Menu::with_items(
        app,
        &[
            &header_item,
            &sep1,
            &open_item,
            &toggle_item,
            &sep2,
            &settings_item,
            &sep3,
            &quit_item,
        ],
    )?;

    // Store a reference to update the checkmark when toggled from frontend or IPC
    let _ = TRAY_TOGGLE_ITEM.set(toggle_item.clone());

    let icon = app
        .default_window_icon()
        .cloned()
        .ok_or("Default window icon not found in application bundle")?;

    let _tray = TrayIconBuilder::with_id("curry-tray")
        .icon(icon)
        .menu(&menu)
        .tooltip("Curry")
        .show_menu_on_left_click(false)
        .on_menu_event(move |app, event| {
            match event.id.as_ref() {
                "open" => {
                    restore_main_window(app);
                }
                "toggle_enabled" => {
                    let state = app.state::<AppState>();
                    let next_state = state.toggle_enabled();
                    let _ = toggle_item.set_checked(next_state);
                    let _ = app.emit("app-state-changed", next_state);
                }
                "settings" => {
                    restore_main_window(app);
                    let _ = app.emit("open-settings-tab", ());
                }
                "quit" => {
                    if let Some(state) = app.try_state::<crate::state::AppState>() {
                        if let Some(engine) = state.notification_engine() {
                            let _ = engine.stop_listening();
                        }
                    }
                    app.exit(0);
                }
                _ => {}
            }
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click {
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                let app = tray.app_handle();
                restore_main_window(app);
            }
        })
        .build(app)?;

    Ok(())
}
