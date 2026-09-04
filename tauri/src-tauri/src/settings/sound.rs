/// Native notification sound alert player.
pub struct SoundManager;

impl SoundManager {
    /// Plays the standard desktop notification / information chime.
    pub fn play_alert() {
        #[cfg(target_os = "windows")]
        {
            unsafe {
                extern "system" {
                    fn MessageBeep(u_type: u32) -> i32;
                }
                // 0x00000040 = MB_ICONASTERISK (standard SystemAsterisk / notification sound)
                MessageBeep(0x00000040);
            }
        }
    }
}
