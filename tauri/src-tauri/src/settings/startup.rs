use std::process::Command;

#[cfg(target_os = "windows")]
fn reg_command() -> Command {
    use std::os::windows::process::CommandExt;
    const CREATE_NO_WINDOW: u32 = 0x08000000;

    let mut cmd = Command::new("reg");
    cmd.creation_flags(CREATE_NO_WINDOW);
    cmd
}

/// Platform-agnostic startup preference manager.
pub struct StartupManager;

impl StartupManager {
    /// Queries whether Curry is currently configured to run on user login.
    pub fn is_enabled() -> bool {
        #[cfg(target_os = "windows")]
        {
            let curry_check = reg_command()
                .args([
                    "query",
                    "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                    "/v",
                    "Curry",
                ])
                .output();

            if let Ok(out) = curry_check {
                if out.status.success() {
                    return true;
                }
            }

            // [LEGACY / BACKWARDS COMPATIBILITY] Fallback check for legacy NotiGlow registration
            let notiglow_check = reg_command()
                .args([
                    "query",
                    "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                    "/v",
                    "NotiGlow",
                ])
                .output();

            match notiglow_check {
                Ok(out) => out.status.success(),
                Err(_) => false,
            }
        }
        #[cfg(not(target_os = "windows"))]
        {
            false
        }
    }

    /// Sets or removes the autostart registration for the current user.
    pub fn set_enabled(enabled: bool) -> Result<(), String> {
        #[cfg(target_os = "windows")]
        {
            if enabled {
                let current_exe = std::env::current_exe()
                    .map_err(|e| format!("Failed to determine current executable path: {}", e))?;
                let exe_str = current_exe.to_string_lossy();
                let command_val = format!("\"{}\" --autostart", exe_str);

                // [LEGACY / BACKWARDS COMPATIBILITY] Clean up any legacy GlowBorder or NotiGlow startup entries
                let _ = reg_command()
                    .args([
                        "delete",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "GlowBorder",
                        "/f",
                    ])
                    .output();
                let _ = reg_command()
                    .args([
                        "delete",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "NotiGlow",
                        "/f",
                    ])
                    .output();

                let status = reg_command()
                    .args([
                        "add",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "Curry",
                        "/t",
                        "REG_SZ",
                        "/d",
                        &command_val,
                        "/f",
                    ])
                    .status()
                    .map_err(|e| format!("Failed to execute reg.exe: {}", e))?;

                if status.success() {
                    Ok(())
                } else {
                    Err("Failed to set Windows Run registry key".to_string())
                }
            } else {
                let _ = reg_command()
                    .args([
                        "delete",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "GlowBorder",
                        "/f",
                    ])
                    .output();

                // [LEGACY / BACKWARDS COMPATIBILITY] Remove legacy NotiGlow Run entry when disabling autostart
                let _ = reg_command()
                    .args([
                        "delete",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "NotiGlow",
                        "/f",
                    ])
                    .output();

                let output = reg_command()
                    .args([
                        "delete",
                        "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "/v",
                        "Curry",
                        "/f",
                    ])
                    .output()
                    .map_err(|e| format!("Failed to execute reg.exe: {}", e))?;

                let _ = output;
                Ok(())
            }
        }
        #[cfg(not(target_os = "windows"))]
        {
            let _ = enabled;
            Err("Startup configuration is only supported on Windows in this release".to_string())
        }
    }
}
