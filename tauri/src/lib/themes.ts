export type ThemeId =
  | 'catppuccin'
  | 'vintage-paper'
  | 'amethyst-haze'
  | 'sage-mist'
  | 'bubblegum'
  | 'perpetuity'
  | 'amberstate';

export interface ThemeDefinition {
  id: ThemeId;
  name: string;
  label: string;
  description: string;
  swatches: string[];
  glowPalette: string[];
  colors: {
    background: string;
    backgroundSecondary: string;
    surface: string;
    surfaceElevated: string;
    text: string;
    textMuted: string;
    border: string;
    accent: string;
    accentSecondary: string;
    success: string;
    warning: string;
    danger: string;
  };
}

export const DEFAULT_THEME: ThemeId = 'perpetuity';

export const THEMES: ThemeDefinition[] = [
  {
    id: 'perpetuity',
    name: 'Perpetuity',
    label: 'Perpetuity',
    description: 'Precision obsidian slate & futuristic cyan-indigo glow',
    swatches: ['#0b0f19', '#121826', '#6366f1', '#38bdf8'],
    glowPalette: ['#6366f1', '#38bdf8', '#818cf8', '#0ea5e9', '#06b6d4', '#a5b4fc', '#e0e7ff'],
    colors: {
      background: '#0b0f19',
      backgroundSecondary: '#070a11',
      surface: '#121826',
      surfaceElevated: '#1a2336',
      text: '#f8fafc',
      textMuted: '#94a3b8',
      border: 'rgba(255, 255, 255, 0.08)',
      accent: '#6366f1',
      accentSecondary: '#38bdf8',
      success: '#10b981',
      warning: '#f59e0b',
      danger: '#ef4444',
    },
  },
  {
    id: 'catppuccin',
    name: 'Catppuccin',
    label: 'Catppuccin',
    description: 'Soothing pastel lavender, mocha crust & soft mauve',
    swatches: ['#181825', '#1e1e2e', '#cba6f7', '#89b4fa'],
    glowPalette: ['#cba6f7', '#89b4fa', '#f5c2e7', '#94e2d5', '#b4befe', '#eba0ac', '#f2cdcd'],
    colors: {
      background: '#181825',
      backgroundSecondary: '#11111b',
      surface: '#1e1e2e',
      surfaceElevated: '#313244',
      text: '#cdd6f4',
      textMuted: '#a6adc8',
      border: 'rgba(88, 91, 112, 0.35)',
      accent: '#cba6f7',
      accentSecondary: '#89b4fa',
      success: '#a6e3a1',
      warning: '#f9e2af',
      danger: '#f38ba8',
    },
  },
  {
    id: 'vintage-paper',
    name: 'Vintage Paper',
    label: 'Vintage Paper',
    description: 'Editorial warm parchment, sepia body & rich dark ink',
    swatches: ['#f4efe6', '#f7f3ec', '#8b5e34', '#b8860b'],
    glowPalette: ['#8b5e34', '#b8860b', '#2d6a4f', '#d4a373', '#9c6644', '#7f4f24', '#c68b59'],
    colors: {
      background: '#f4efe6',
      backgroundSecondary: '#ebe4d8',
      surface: '#f7f3ec',
      surfaceElevated: '#ded6c7',
      text: '#241e17',
      textMuted: '#68594c',
      border: 'rgba(168, 150, 130, 0.38)',
      accent: '#8b5e34',
      accentSecondary: '#b8860b',
      success: '#2d6a4f',
      warning: '#b8860b',
      danger: '#b91c1c',
    },
  },
  {
    id: 'amethyst-haze',
    name: 'Amethyst Haze',
    label: 'Amethyst Haze',
    description: 'Mystical violet aura, void purple & vivid amethyst',
    swatches: ['#0e0b16', '#191329', '#a855f7', '#ec4899'],
    glowPalette: ['#a855f7', '#ec4899', '#c084fc', '#d946ef', '#8b5cf6', '#7c3aed', '#f472b6'],
    colors: {
      background: '#0e0b16',
      backgroundSecondary: '#08060c',
      surface: '#191329',
      surfaceElevated: '#281c40',
      text: '#f5f0ff',
      textMuted: '#c084fc',
      border: 'rgba(168, 85, 247, 0.22)',
      accent: '#a855f7',
      accentSecondary: '#ec4899',
      success: '#34d399',
      warning: '#fbbf24',
      danger: '#f43f5e',
    },
  },
  {
    id: 'sage-mist',
    name: 'Sage Mist',
    label: 'Sage Mist',
    description: 'Calm botanical forest, evergreen tones & sage mint',
    swatches: ['#0c1310', '#121e18', '#34d399', '#14b8a6'],
    glowPalette: ['#34d399', '#14b8a6', '#10b981', '#059669', '#6ee7b7', '#2dd4bf', '#a7f3d0'],
    colors: {
      background: '#0c1310',
      backgroundSecondary: '#070b09',
      surface: '#121e18',
      surfaceElevated: '#1a2c24',
      text: '#ecfdf5',
      textMuted: '#a7f3d0',
      border: 'rgba(52, 211, 153, 0.22)',
      accent: '#34d399',
      accentSecondary: '#14b8a6',
      success: '#10b981',
      warning: '#f59e0b',
      danger: '#f87171',
    },
  },
  {
    id: 'bubblegum',
    name: 'Bubblegum',
    label: 'Bubblegum',
    description: 'Vibrant cyber-pop neon rose, velvet berry & berry noir',
    swatches: ['#140a14', '#221022', '#f43f5e', '#c084fc'],
    glowPalette: ['#f43f5e', '#c084fc', '#fb7185', '#e11d48', '#ec4899', '#f472b6', '#fda4af'],
    colors: {
      background: '#140a14',
      backgroundSecondary: '#0c050c',
      surface: '#221022',
      surfaceElevated: '#321832',
      text: '#fdf2f8',
      textMuted: '#fda4af',
      border: 'rgba(244, 63, 94, 0.22)',
      accent: '#f43f5e',
      accentSecondary: '#c084fc',
      success: '#4ade80',
      warning: '#facc15',
      danger: '#e11d48',
    },
  },
  {
    id: 'amberstate',
    name: 'Amber Slate',
    label: 'Amberstate',
    description: 'Molten warm amber & industrial slate steel granite',
    swatches: ['#0d1117', '#161b22', '#f59e0b', '#ea580c'],
    glowPalette: ['#f59e0b', '#ea580c', '#d97706', '#fbbf24', '#f97316', '#c2410c', '#fde68a'],
    colors: {
      background: '#0d1117',
      backgroundSecondary: '#080a0e',
      surface: '#161b22',
      surfaceElevated: '#212833',
      text: '#f8fafc',
      textMuted: '#cbd5e1',
      border: 'rgba(245, 158, 11, 0.22)',
      accent: '#f59e0b',
      accentSecondary: '#ea580c',
      success: '#10b981',
      warning: '#f59e0b',
      danger: '#ef4444',
    },
  },
];

export function getThemeDefinition(id: ThemeId | string): ThemeDefinition {
  return THEMES.find((t) => t.id === id) || THEMES[0];
}

export function getThemeGlowPalette(themeId: ThemeId | string): string[] {
  const theme = getThemeDefinition(themeId);
  return theme.glowPalette || THEMES[0].glowPalette;
}
