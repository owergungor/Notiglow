use serde::{Deserialize, Deserializer, Serialize};
use serde_json::Value;
use std::collections::HashMap;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub enum GlowStyle {
    Pulse,
    Sweep,
    Ambient,
    Comet,
    Ripple,
}

impl Default for GlowStyle {
    fn default() -> Self {
        GlowStyle::Pulse
    }
}

impl<'de> Deserialize<'de> for GlowStyle {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let v = Value::deserialize(deserializer)?;
        Ok(match &v {
            Value::String(s) => match s.trim().to_ascii_lowercase().as_str() {
                "sweep" => GlowStyle::Sweep,
                "ambient" => GlowStyle::Ambient,
                "comet" => GlowStyle::Comet,
                "ripple" => GlowStyle::Ripple,
                _ => GlowStyle::Pulse,
            },
            Value::Number(n) => match n.as_i64().unwrap_or(0) {
                1 => GlowStyle::Sweep,
                2 => GlowStyle::Ambient,
                3 => GlowStyle::Comet,
                4 => GlowStyle::Ripple,
                _ => GlowStyle::Pulse,
            },
            _ => GlowStyle::Pulse,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub enum NotificationPriority {
    Low,
    Normal,
    High,
}

impl Default for NotificationPriority {
    fn default() -> Self {
        NotificationPriority::Normal
    }
}

impl<'de> Deserialize<'de> for NotificationPriority {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let v = Value::deserialize(deserializer)?;
        Ok(match &v {
            Value::String(s) => match s.trim().to_ascii_lowercase().as_str() {
                "low" => NotificationPriority::Low,
                "high" => NotificationPriority::High,
                _ => NotificationPriority::Normal,
            },
            Value::Number(n) => match n.as_i64().unwrap_or(1) {
                0 => NotificationPriority::Low,
                2 => NotificationPriority::High,
                _ => NotificationPriority::Normal,
            },
            _ => NotificationPriority::Normal,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub enum MonitorMode {
    ActiveMonitor,
    PrimaryMonitor,
    AllMonitors,
}

impl Default for MonitorMode {
    fn default() -> Self {
        MonitorMode::ActiveMonitor
    }
}

impl<'de> Deserialize<'de> for MonitorMode {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let v = Value::deserialize(deserializer)?;
        Ok(match &v {
            Value::String(s) => match s.trim().to_ascii_lowercase().as_str() {
                "primarymonitor" | "primary" => MonitorMode::PrimaryMonitor,
                "allmonitors" | "all" => MonitorMode::AllMonitors,
                _ => MonitorMode::ActiveMonitor,
            },
            Value::Number(n) => match n.as_i64().unwrap_or(0) {
                1 => MonitorMode::PrimaryMonitor,
                2 => MonitorMode::AllMonitors,
                _ => MonitorMode::ActiveMonitor,
            },
            _ => MonitorMode::ActiveMonitor,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub enum BurstMode {
    Restart,
    Extend,
    Queue,
    Ignore,
}

impl Default for BurstMode {
    fn default() -> Self {
        BurstMode::Restart
    }
}

impl<'de> Deserialize<'de> for BurstMode {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let v = Value::deserialize(deserializer)?;
        Ok(match &v {
            Value::String(s) => match s.trim().to_ascii_lowercase().as_str() {
                "extend" => BurstMode::Extend,
                "queue" => BurstMode::Queue,
                "ignore" => BurstMode::Ignore,
                _ => BurstMode::Restart,
            },
            Value::Number(n) => match n.as_i64().unwrap_or(0) {
                1 => BurstMode::Extend,
                2 => BurstMode::Queue,
                3 => BurstMode::Ignore,
                _ => BurstMode::Restart,
            },
            _ => BurstMode::Restart,
        })
    }
}

fn default_true() -> bool {
    true
}
fn default_color_hex() -> String {
    "#FFFFFF".to_string()
}
fn default_duration_ms() -> i32 {
    4000
}
fn default_intensity() -> f64 {
    0.8
}
fn default_thickness() -> f64 {
    4.0
}
fn default_glow_size() -> f64 {
    32.0
}
fn default_speed() -> f64 {
    1.0
}
fn default_gaming_intensity_mult() -> f64 {
    0.6
}
fn default_gaming_duration_mult() -> f64 {
    0.5
}

// Lossless C#-compatible GameGlowOverride
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[allow(non_snake_case)]
pub struct GameGlowOverride {
    #[serde(default)]
    pub GameIdentifier: String,
    #[serde(default)]
    pub CustomGlowEnabled: bool,
    pub DurationMs: Option<i32>,
    pub Intensity: Option<f64>,
    pub GlowSize: Option<f64>,
    pub CornerRadius: Option<f64>,
    pub ColorHex: Option<String>,
    pub Style: Option<GlowStyle>,
    #[serde(flatten)]
    pub extra: HashMap<String, Value>,
}

impl GameGlowOverride {
    pub fn clamp_duration(duration_ms: i32) -> i32 {
        duration_ms.clamp(500, 15000)
    }
    pub fn clamp_intensity(intensity: f64) -> f64 {
        intensity.clamp(0.1, 1.0)
    }
    pub fn clamp_glow_size(glow_size: f64) -> f64 {
        glow_size.clamp(0.0, 256.0)
    }
    pub fn clamp_corner_radius(radius: f64) -> f64 {
        radius.clamp(0.0, 48.0)
    }
}

// Lossless C#-compatible AppSettings (preserves all unknown fields via #[serde(flatten)])
#[derive(Debug, Clone, Serialize, Deserialize)]
#[allow(non_snake_case)]
pub struct AppSettings {
    #[serde(default = "default_true")]
    pub MasterEnabled: bool,
    #[serde(default)]
    pub ReduceAnimations: bool,
    #[serde(default)]
    pub ReduceMotion: bool,
    #[serde(default)]
    pub ReduceGlow: bool,
    #[serde(default)]
    pub OledMode: bool,
    #[serde(default)]
    pub CornerRadius: f64,
    #[serde(default)]
    pub MonitorMode: MonitorMode,
    #[serde(default)]
    pub BurstMode: BurstMode,

    // v5.0 Rust Architecture Feature Flags (default true — primary runtime engine)
    #[serde(default = "default_true")]
    pub UseRustBackgroundWorker: bool,
    #[serde(default = "default_true")]
    pub UseRustGameDetection: bool,
    #[serde(default = "default_true")]
    pub UseRustGlowEngine: bool,

    // Gaming Mode Settings
    #[serde(default)]
    pub GamingModeEnabled: bool,
    #[serde(default = "default_true")]
    pub GlowDuringGames: bool,
    #[serde(default = "default_true")]
    pub ReduceIntensityInGames: bool,
    #[serde(default = "default_gaming_intensity_mult")]
    pub GamingIntensityMultiplier: f64,
    #[serde(default = "default_true")]
    pub ReduceDurationInGames: bool,
    #[serde(default = "default_gaming_duration_mult")]
    pub GamingDurationMultiplier: f64,
    #[serde(default)]
    pub OnlyImportantInGames: bool,
    #[serde(default)]
    pub TrackedGames: Vec<String>,
    #[serde(default)]
    pub IgnoredGames: Vec<String>,

    // Shared Gaming Glow Settings
    pub CommonGamingDurationMs: Option<i32>,
    pub CommonGamingIntensity: Option<f64>,
    pub CommonGamingGlowSize: Option<f64>,
    pub CommonGamingCornerRadius: Option<f64>,
    #[serde(default)]
    pub HasCustomCommonGamingSettings: bool,

    #[serde(default)]
    pub GameOverrides: HashMap<String, GameGlowOverride>,

    // Global defaults
    #[serde(default = "default_color_hex")]
    pub DefaultColorHex: String,
    #[serde(default = "default_duration_ms")]
    pub DefaultDurationMs: i32,
    #[serde(default = "default_intensity")]
    pub DefaultIntensity: f64,
    #[serde(default = "default_thickness")]
    pub DefaultThickness: f64,
    #[serde(default = "default_glow_size")]
    pub DefaultGlowSize: f64,
    #[serde(default)]
    pub DefaultStyle: GlowStyle,

    #[serde(flatten)]
    pub extra: HashMap<String, Value>,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            MasterEnabled: true,
            ReduceAnimations: false,
            ReduceMotion: false,
            ReduceGlow: false,
            OledMode: false,
            CornerRadius: 0.0,
            MonitorMode: MonitorMode::ActiveMonitor,
            BurstMode: BurstMode::Restart,
            UseRustBackgroundWorker: true,
            UseRustGameDetection: true,
            UseRustGlowEngine: true,
            GamingModeEnabled: false,
            GlowDuringGames: true,
            ReduceIntensityInGames: true,
            GamingIntensityMultiplier: 0.6,
            ReduceDurationInGames: true,
            GamingDurationMultiplier: 0.5,
            OnlyImportantInGames: false,
            TrackedGames: Vec::new(),
            IgnoredGames: Vec::new(),
            CommonGamingDurationMs: Some(3000),
            CommonGamingIntensity: Some(0.8),
            CommonGamingGlowSize: Some(32.0),
            CommonGamingCornerRadius: Some(0.0),
            HasCustomCommonGamingSettings: false,
            GameOverrides: HashMap::new(),
            DefaultColorHex: "#FFFFFF".to_string(),
            DefaultDurationMs: 4000,
            DefaultIntensity: 0.8,
            DefaultThickness: 4.0,
            DefaultGlowSize: 32.0,
            DefaultStyle: GlowStyle::Pulse,
            extra: HashMap::new(),
        }
    }
}

impl AppSettings {
    pub fn normalize_game_identifier(game_path_or_name: &str) -> String {
        let trimmed = game_path_or_name.trim();
        if trimmed.is_empty() {
            return String::new();
        }
        let file_name = std::path::Path::new(trimmed)
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or(trimmed);
        file_name.to_ascii_lowercase()
    }

    pub fn find_game_override(&self, active_id: &str) -> Option<&GameGlowOverride> {
        let norm = Self::normalize_game_identifier(active_id);
        if norm.is_empty() {
            return None;
        }
        // Case-insensitive lookup matching C# Dictionary<string, GameGlowOverride>(OrdinalIgnoreCase)
        self.GameOverrides
            .iter()
            .find(|(k, _)| Self::normalize_game_identifier(k) == norm)
            .map(|(_, v)| v)
    }
}

fn clean_identity_string(input: &str) -> String {
    let trimmed = input.trim();
    if trimmed.len() >= 4 && trimmed[trimmed.len() - 4..].eq_ignore_ascii_case(".exe") {
        trimmed[..trimmed.len() - 4].to_string()
    } else {
        trimmed.to_string()
    }
}

fn matches_any_ci(input: &str, candidates: &[&str]) -> bool {
    if input.is_empty() {
        return false;
    }
    candidates
        .iter()
        .any(|c| !c.is_empty() && c.eq_ignore_ascii_case(input))
}

/// Exact port of C# ProfileService.IsWordOrPackageMatch:
/// - Target < 3 chars requires exact case-insensitive match.
/// - Target >= 3 chars requires non-alphanumeric boundaries on both sides.
pub fn is_word_or_package_match(source: &str, target: &str) -> bool {
    if source.is_empty() || target.is_empty() {
        return false;
    }
    if target.chars().count() < 3 {
        return source.eq_ignore_ascii_case(target);
    }

    let src_lower = source.to_lowercase();
    let tgt_lower = target.to_lowercase();
    let mut search_from = 0usize;

    while let Some(rel_idx) = src_lower[search_from..].find(&tgt_lower) {
        let start = search_from + rel_idx;
        let end = start + tgt_lower.len();

        let left_boundary = if start == 0 {
            true
        } else {
            src_lower[..start]
                .chars()
                .next_back()
                .map(|c| !c.is_alphanumeric())
                .unwrap_or(true)
        };

        let right_boundary = if end >= src_lower.len() {
            true
        } else {
            src_lower[end..]
                .chars()
                .next()
                .map(|c| !c.is_alphanumeric())
                .unwrap_or(true)
        };

        if left_boundary && right_boundary {
            return true;
        }

        // Advance by one char to inspect any further occurrences
        let step = src_lower[start..]
            .chars()
            .next()
            .map(|c| c.len_utf8())
            .unwrap_or(1);
        search_from = start + step;
    }

    false
}

/// Exact port of C# ProfileService.GetProfile(appId, appName):
/// - Tier 1: Exact match across raw/clean AppId, Name, and ExecutablePath filename.
/// - Tier 2: Word/package boundary match (IsWordOrPackageMatch, min 3 chars) across clean/raw inputs.
pub fn match_profile<'a>(
    profiles: &'a [AppProfile],
    app_id: &str,
    app_name: &str,
) -> Option<&'a AppProfile> {
    let raw_app_id = app_id.trim();
    let clean_app_id = clean_identity_string(raw_app_id);
    let raw_app_name = app_name.trim();
    let clean_app_name = clean_identity_string(raw_app_name);

    if raw_app_id.is_empty() && raw_app_name.is_empty() {
        return None;
    }

    // Tier 1: Exact match (preserving profile list priority order)
    for p in profiles {
        let p_app_id = p.AppId.trim();
        let p_clean_app_id = clean_identity_string(p_app_id);
        let p_name = p.Name.trim();
        let p_clean_name = clean_identity_string(p_name);
        let p_exe_name = p
            .ExecutablePath
            .as_deref()
            .and_then(|e| std::path::Path::new(e).file_name().and_then(|s| s.to_str()))
            .unwrap_or("");
        let p_clean_exe_name = clean_identity_string(p_exe_name);

        let cands = [
            p_app_id,
            p_clean_app_id.as_str(),
            p_name,
            p_clean_name.as_str(),
            p_exe_name,
            p_clean_exe_name.as_str(),
        ];

        if matches_any_ci(raw_app_id, &cands)
            || matches_any_ci(&clean_app_id, &cands)
            || matches_any_ci(raw_app_name, &cands)
            || matches_any_ci(&clean_app_name, &cands)
        {
            return Some(p);
        }
    }

    // Tier 2: Word / package boundary match (AUMID / Package Family Name / DisplayName)
    for p in profiles {
        let target_app_id = clean_identity_string(&p.AppId);
        let target_name = clean_identity_string(&p.Name);
        let target_exe = p
            .ExecutablePath
            .as_deref()
            .and_then(|e| std::path::Path::new(e).file_name().and_then(|s| s.to_str()))
            .map(clean_identity_string)
            .unwrap_or_default();

        for src in [clean_app_id.as_str(), clean_app_name.as_str(), raw_app_id] {
            if src.is_empty() {
                continue;
            }
            if is_word_or_package_match(src, &target_app_id)
                || is_word_or_package_match(src, &target_name)
                || (!target_exe.is_empty() && is_word_or_package_match(src, &target_exe))
            {
                return Some(p);
            }
        }
    }

    None
}

// Lossless C#-compatible AppProfile
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[allow(non_snake_case)]
pub struct AppProfile {
    #[serde(default)]
    pub AppId: String,
    #[serde(default)]
    pub Name: String,
    pub ExecutablePath: Option<String>,
    #[serde(default = "default_true")]
    pub Enabled: bool,
    #[serde(default = "default_color_hex")]
    pub ColorHex: String,
    #[serde(default = "default_duration_ms")]
    pub DurationMs: i32,
    #[serde(default = "default_intensity")]
    pub Intensity: f64,
    #[serde(default = "default_thickness")]
    pub Thickness: f64,
    #[serde(default = "default_glow_size")]
    pub GlowSize: f64,
    #[serde(default)]
    pub CornerRadius: f64,
    #[serde(default)]
    pub Style: GlowStyle,
    #[serde(default)]
    pub Priority: NotificationPriority,
    #[serde(default)]
    pub Category: String,
    #[serde(default)]
    pub IsCustom: bool,
    #[serde(default = "default_speed")]
    pub Speed: f64,
    #[serde(default)]
    pub CoreBrightness: f64,
    #[serde(default)]
    pub TrailLength: f64,
    #[serde(flatten)]
    pub extra: HashMap<String, Value>,
}

impl Default for AppProfile {
    fn default() -> Self {
        Self {
            AppId: String::new(),
            Name: "Test".to_string(),
            ExecutablePath: None,
            Enabled: true,
            ColorHex: "#00E5FF".to_string(),
            DurationMs: 1200,
            Intensity: 0.85,
            Thickness: 4.0,
            GlowSize: 36.0,
            CornerRadius: 0.0,
            Style: GlowStyle::Pulse,
            Priority: NotificationPriority::Normal,
            Category: String::new(),
            IsCustom: false,
            Speed: 1.0,
            CoreBrightness: 1.0,
            TrailLength: 0.5,
            extra: HashMap::new(),
        }
    }
}

/// Exact ColorHelper.RgbSpectrumColors from production C#
pub const RGB_SPECTRUM_COLORS: [(u8, u8, u8); 9] = [
    (255, 0, 77),   // Pink/Red
    (255, 122, 0),  // Orange
    (255, 214, 0),  // Yellow
    (0, 230, 118),  // Green
    (0, 229, 255),  // Cyan
    (41, 121, 255), // Blue
    (124, 58, 237), // Purple
    (255, 0, 128),  // Magenta
    (255, 0, 77),   // Loop back to Pink/Red
];

pub fn is_rgb_spectrum(input: &str) -> bool {
    let t = input.trim();
    t.eq_ignore_ascii_case("RGB")
        || t.eq_ignore_ascii_case("#RGB")
        || t.eq_ignore_ascii_case("Rainbow")
}

/// Port of ColorHelper.ParseColor (supports #RGB, #RRGGBB, #AARRGGBB, rgb(r,g,b))
pub fn parse_color_rgb(input: &str) -> (u8, u8, u8) {
    let trimmed = input.trim();
    if trimmed.is_empty() {
        return (88, 101, 242); // #5865F2 fallback
    }
    if is_rgb_spectrum(trimmed) {
        return (255, 0, 77);
    }

    // Handle rgb(r, g, b)
    let lower = trimmed.to_ascii_lowercase();
    if let Some(rest) = lower.strip_prefix("rgb") {
        let rest = rest.trim();
        if let Some(inner) = rest.strip_prefix('(').and_then(|s| s.strip_suffix(')')) {
            let parts: Vec<&str> = inner.split(',').map(|s| s.trim()).collect();
            if parts.len() == 3 {
                if let (Ok(r), Ok(g), Ok(b)) = (
                    parts[0].parse::<u8>(),
                    parts[1].parse::<u8>(),
                    parts[2].parse::<u8>(),
                ) {
                    return (r, g, b);
                }
            }
        }
    }

    let clean = trimmed.trim_start_matches('#');
    if clean.len() == 3 {
        let b = clean.as_bytes();
        let parse_nibble = |c: u8| -> Option<u8> {
            let s = [c, c];
            u8::from_str_radix(std::str::from_utf8(&s).ok()?, 16).ok()
        };
        if let (Some(r), Some(g), Some(bl)) =
            (parse_nibble(b[0]), parse_nibble(b[1]), parse_nibble(b[2]))
        {
            return (r, g, bl);
        }
    } else if clean.len() == 6 {
        if let (Ok(r), Ok(g), Ok(b)) = (
            u8::from_str_radix(&clean[0..2], 16),
            u8::from_str_radix(&clean[2..4], 16),
            u8::from_str_radix(&clean[4..6], 16),
        ) {
            return (r, g, b);
        }
    } else if clean.len() == 8 {
        if let (Ok(r), Ok(g), Ok(b)) = (
            u8::from_str_radix(&clean[2..4], 16),
            u8::from_str_radix(&clean[4..6], 16),
            u8::from_str_radix(&clean[6..8], 16),
        ) {
            return (r, g, b);
        }
    }

    (88, 101, 242)
}

#[derive(Debug, Clone, PartialEq)]
pub enum TriggerDecision {
    Suppressed(&'static str),
    Play {
        adjusted: AppProfile,
        reduce_motion_static: bool,
    },
}

/// Exact port of GlowManager.TriggerProfileCore suppression + GlowManager.AdjustProfileParameters
pub fn evaluate_and_adjust_profile(
    original: &AppProfile,
    settings: &AppSettings,
    is_game_running: bool,
    active_game_id: Option<&str>,
    is_test: bool,
) -> TriggerDecision {
    if !settings.MasterEnabled {
        return TriggerDecision::Suppressed("MasterDisabled");
    }

    if is_game_running {
        if !settings.GlowDuringGames {
            return TriggerDecision::Suppressed("GlowDuringGamesDisabled");
        }
        if settings.OnlyImportantInGames && original.Priority != NotificationPriority::High {
            return TriggerDecision::Suppressed("OnlyImportantInGames");
        }
    }

    let mut intensity = original.Intensity;
    let mut duration = original.DurationMs;
    let mut glow_size = original.GlowSize;
    let mut corner_radius = if original.CornerRadius > 0.0 {
        original.CornerRadius
    } else {
        settings.CornerRadius
    };
    let mut color_hex = original.ColorHex.clone();
    let mut style = original.Style;

    // Gaming mode scaling & per-game overrides (exact C# Priority 1 -> Priority 2 hierarchy)
    let is_gaming_active = is_test || (is_game_running && settings.GamingModeEnabled);
    if is_gaming_active {
        let game_override = active_game_id.and_then(|id| settings.find_game_override(id));

        if let Some(ov) = game_override.filter(|o| o.CustomGlowEnabled) {
            // Priority 1: Per-game Custom Glow ON
            if let Some(d) = ov.DurationMs {
                duration = GameGlowOverride::clamp_duration(d);
            }
            if let Some(i) = ov.Intensity {
                intensity = GameGlowOverride::clamp_intensity(i);
            }
            if let Some(g) = ov.GlowSize {
                glow_size = GameGlowOverride::clamp_glow_size(g);
            }
            if let Some(c) = ov.CornerRadius {
                corner_radius = GameGlowOverride::clamp_corner_radius(c);
            }
            if let Some(ref hex) = ov.ColorHex {
                if !hex.trim().is_empty() {
                    color_hex = hex.clone();
                }
            }
            if let Some(st) = ov.Style {
                style = st;
            }
        } else {
            // Priority 2: Global Gaming Settings
            if let Some(d) = settings.CommonGamingDurationMs {
                duration = GameGlowOverride::clamp_duration(d);
            }
            if let Some(i) = settings.CommonGamingIntensity {
                intensity = GameGlowOverride::clamp_intensity(i);
            }
            if let Some(g) = settings.CommonGamingGlowSize {
                glow_size = GameGlowOverride::clamp_glow_size(g);
            }
            if let Some(c) = settings.CommonGamingCornerRadius {
                corner_radius = GameGlowOverride::clamp_corner_radius(c);
            }

            if !settings.HasCustomCommonGamingSettings && settings.ReduceDurationInGames {
                duration = (duration as f64 * settings.GamingDurationMultiplier.clamp(0.2, 1.0))
                    as i32;
            }
            if !settings.HasCustomCommonGamingSettings && settings.ReduceIntensityInGames {
                intensity *= settings.GamingIntensityMultiplier.clamp(0.2, 1.0);
            }
        }
    }

    // OLED Mode scaling
    if settings.OledMode {
        intensity = intensity.min(0.5);
    }

    // Accessibility Reduce Glow & Reduce Motion
    if settings.ReduceGlow {
        intensity = intensity.min(0.35);
    }

    let reduce_motion_static = settings.ReduceMotion || settings.ReduceAnimations;
    if reduce_motion_static {
        if matches!(style, GlowStyle::Sweep | GlowStyle::Comet | GlowStyle::Ripple) {
            style = GlowStyle::Ambient;
        }
        duration = duration.min(2500);
    }

    let mut adjusted = original.clone();
    adjusted.ColorHex = color_hex;
    adjusted.DurationMs = duration.max(500);
    adjusted.Intensity = intensity.clamp(0.05, 1.0);
    adjusted.GlowSize = glow_size.clamp(0.0, 256.0);
    adjusted.CornerRadius = corner_radius.clamp(0.0, 48.0);
    adjusted.Style = style;

    TriggerDecision::Play {
        adjusted,
        reduce_motion_static,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_color_parsing_and_rgb_detection() {
        assert!(is_rgb_spectrum("RGB"));
        assert!(is_rgb_spectrum("#rgb"));
        assert!(is_rgb_spectrum("Rainbow"));
        assert!(!is_rgb_spectrum("#00E5FF"));
        assert_eq!(parse_color_rgb("#00E5FF"), (0, 229, 255));
        assert_eq!(parse_color_rgb("#F05"), (255, 0, 85));
        assert_eq!(parse_color_rgb("rgb(12, 34, 56)"), (12, 34, 56));
        assert_eq!(parse_color_rgb("#80FF1020"), (255, 16, 32));
    }

    #[test]
    fn test_oled_and_reduce_motion_parity() {
        let mut settings = AppSettings::default();
        settings.OledMode = true;
        settings.ReduceMotion = true;

        let mut profile = AppProfile::default();
        profile.Style = GlowStyle::Comet;
        profile.DurationMs = 4000;
        profile.Intensity = 0.95;

        match evaluate_and_adjust_profile(&profile, &settings, false, None, false) {
            TriggerDecision::Play {
                adjusted,
                reduce_motion_static,
            } => {
                assert!(reduce_motion_static);
                assert_eq!(adjusted.Style, GlowStyle::Ambient);
                assert_eq!(adjusted.DurationMs, 2500);
                assert!((adjusted.Intensity - 0.5).abs() < 1e-6);
            }
            _ => panic!("Expected Play"),
        }
    }

    #[test]
    fn test_game_override_priority_and_suppression() {
        let mut settings = AppSettings::default();
        settings.GamingModeEnabled = true;
        settings.GlowDuringGames = true;
        settings.OnlyImportantInGames = true;
        settings.GameOverrides.insert(
            "cs2.exe".to_string(),
            GameGlowOverride {
                GameIdentifier: "cs2.exe".to_string(),
                CustomGlowEnabled: true,
                DurationMs: Some(1800),
                Intensity: Some(0.75),
                GlowSize: Some(48.0),
                CornerRadius: Some(24.0),
                ColorHex: Some("#FF3366".to_string()),
                Style: Some(GlowStyle::Sweep),
                extra: HashMap::new(),
            },
        );

        let mut normal_profile = AppProfile::default();
        normal_profile.Priority = NotificationPriority::Normal;
        assert_eq!(
            evaluate_and_adjust_profile(&normal_profile, &settings, true, Some("CS2.EXE"), false),
            TriggerDecision::Suppressed("OnlyImportantInGames")
        );

        let mut high_profile = AppProfile::default();
        high_profile.Priority = NotificationPriority::High;
        match evaluate_and_adjust_profile(&high_profile, &settings, true, Some("C:\\Games\\CS2.exe"), false) {
            TriggerDecision::Play { adjusted, .. } => {
                assert_eq!(adjusted.DurationMs, 1800);
                assert!((adjusted.Intensity - 0.75).abs() < 1e-6);
                assert!((adjusted.GlowSize - 48.0).abs() < 1e-6);
                assert!((adjusted.CornerRadius - 24.0).abs() < 1e-6);
                assert_eq!(adjusted.ColorHex, "#FF3366");
                assert_eq!(adjusted.Style, GlowStyle::Sweep);
            }
            _ => panic!("Expected Play"),
        }
    }

    #[test]
    fn test_profile_matching_parity_boundaries_exe_priority() {
        let profiles = vec![
            AppProfile {
                AppId: "art".to_string(),
                Name: "Art".to_string(),
                ..Default::default()
            },
            AppProfile {
                AppId: "ai".to_string(),
                Name: "AI".to_string(),
                ..Default::default()
            },
            AppProfile {
                AppId: "Teams".to_string(),
                Name: "Microsoft Teams".to_string(),
                ..Default::default()
            },
            AppProfile {
                AppId: "com.squirrel.Discord.Discord".to_string(),
                Name: "Discord".to_string(),
                ExecutablePath: Some(r"C:\Users\me\AppData\Local\Discord\Discord.exe".to_string()),
                ..Default::default()
            },
            AppProfile {
                AppId: "Party".to_string(),
                Name: "Party App".to_string(),
                ..Default::default()
            },
        ];

        // 1. Short substring false-positive guard (<3 chars & word boundaries)
        assert!(match_profile(&profiles, "mail.exe", "Mail").is_none());
        assert_eq!(match_profile(&profiles, "ai", "").unwrap().AppId, "ai");
        assert!(match_profile(&profiles, "SmartApp", "SmartApp").is_none());

        // 2. Word boundary + Tier 1 exact match beats earlier Tier 2 candidate
        assert_eq!(match_profile(&profiles, "party", "Party").unwrap().AppId, "Party");

        // 3. Package name / AUMID boundary match
        assert_eq!(
            match_profile(&profiles, "Microsoft.Teams_8wekyb3d8bbwe!Teams", "Teams")
                .unwrap()
                .AppId,
            "Teams"
        );

        // 4. Executable filename & case-insensitivity
        assert_eq!(
            match_profile(&profiles, "DISCORD.EXE", "").unwrap().Name,
            "Discord"
        );
        assert_eq!(
            match_profile(&profiles, "discord", "").unwrap().Name,
            "Discord"
        );
    }
}
