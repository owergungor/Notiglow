<script lang="ts">
  import { onMount } from "svelte";
  import { invoke } from "@tauri-apps/api/core";
  import { listen } from "@tauri-apps/api/event";
  import { THEMES, DEFAULT_THEME, type ThemeId, getThemeGlowPalette } from "$lib/themes";

  interface ConnectionInfo {
    connected: boolean;
    app_name: string;
    version: string;
    target_os: string;
    target_arch: string;
    message: string;
  }

  interface Notification {
    id: string;
    title: string;
    message?: string;
    body: string;
    timestamp: number;
    duration?: number | null;
    enabled?: boolean;
    source?: string | null;
    app_name: string;
    source_app?: string;
    icon?: string | null;
    platform: string;
    urgency?: "low" | "normal" | "high" | "critical" | null;
    read: boolean;
  }

  interface GlowSettings {
    enabled: boolean;
    duration_ms: number;
    intensity: number;
    thickness: number;
    corner_radius: number;
    animation_style: "pulse" | "breathing" | "solid";
    monitor_target: "primary" | "active" | "all";
    color: string;
  }

  interface AppSettings {
    enabled: boolean;
    startup_enabled: boolean;
    show_notifications: boolean;
    history_limit: number;
    sound_enabled: boolean;
    glow: GlowSettings;
    theme?: ThemeId;
  }

  type ProviderStatus =
    | "idle"
    | "listening"
    | "permission_required"
    | "permission_denied"
    | "unsupported"
    | { error: string };

  interface PipelineStatus {
    provider_name: string;
    provider_status: ProviderStatus;
    captured_count: number;
    is_enabled: boolean;
  }

  function getAppInitial(name: string | undefined): string {
    if (!name || !name.trim()) return "N";
    return name.trim().charAt(0).toUpperCase();
  }

  function formatTimestamp(ts: number): string {
    if (!ts || isNaN(ts)) return "Just now";
    const elapsedSecs = Math.floor((Date.now() - ts) / 1000);
    if (elapsedSecs < 10) return "Just now";
    if (elapsedSecs < 60) return `${elapsedSecs}s ago`;
    const elapsedMins = Math.floor(elapsedSecs / 60);
    if (elapsedMins < 60) return `${elapsedMins}m ago`;
    const elapsedHours = Math.floor(elapsedMins / 60);
    if (elapsedHours < 24) return `${elapsedHours}h ago`;
    return new Date(ts).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  }

  // Theme Management (Defaults to Perpetuity)
  let selectedTheme = $state<ThemeId>(DEFAULT_THEME);
  let currentGlowPalette = $derived(getThemeGlowPalette(selectedTheme));

  function selectTheme(themeId: ThemeId) {
    selectedTheme = themeId;
    appSettings.theme = themeId;
    saveAppSettings();
    try {
      localStorage.setItem("curry_selected_theme", themeId);
    } catch (err) {
      console.error("Failed to persist theme in localStorage:", err);
    }
  }

  $effect(() => {
    if (typeof document !== "undefined") {
      document.documentElement.setAttribute("data-theme", selectedTheme);
      document.body.setAttribute("data-theme", selectedTheme);
    }
  });

  type TabKey = "dashboard" | "notifications" | "glow" | "settings";

  const TAB_ORDER: Record<TabKey, number> = {
    dashboard: 0,
    notifications: 1,
    glow: 2,
    settings: 3,
  };

  // Top Tab Navigation State with Directional Motion
  let activeTab = $state<TabKey>("dashboard");
  let navDirection = $state<"forward" | "backward" | "none">("none");
  let feedFilter = $state<"all" | "unread" | "critical">("all");

  function navigateToTab(target: TabKey) {
    if (target === activeTab) return;
    const oldIdx = TAB_ORDER[activeTab];
    const newIdx = TAB_ORDER[target];
    navDirection = newIdx > oldIdx ? "forward" : "backward";
    activeTab = target;
  }

  function handleTabKeyDown(e: KeyboardEvent, current: TabKey) {
    const tabs: TabKey[] = ["dashboard", "notifications", "glow", "settings"];
    const currentIdx = TAB_ORDER[current];
    if (e.key === "ArrowRight") {
      e.preventDefault();
      const nextTab = tabs[(currentIdx + 1) % tabs.length];
      navigateToTab(nextTab);
      document.getElementById(`tab-${nextTab}-btn`)?.focus();
    } else if (e.key === "ArrowLeft") {
      e.preventDefault();
      const prevTab = tabs[(currentIdx - 1 + tabs.length) % tabs.length];
      navigateToTab(prevTab);
      document.getElementById(`tab-${prevTab}-btn`)?.focus();
    }
  }

  // Connection & System State
  let status = $state<"checking" | "connected" | "disconnected">("checking");
  let connectionInfo = $state<ConnectionInfo | null>(null);
  let errorMessage = $state<string | null>(null);
  let lastCheckedAt = $state<string | null>(null);
  let pingCount = $state(0);
  let isPinging = $state(false);

  // Application & Settings State
  let isEnabled = $state(true);
  let isToggling = $state(false);
  let appSettings = $state<AppSettings>({
    enabled: true,
    startup_enabled: false,
    show_notifications: true,
    history_limit: 100,
    sound_enabled: false,
    glow: {
      enabled: true,
      duration_ms: 2500,
      intensity: 0.8,
      thickness: 8,
      corner_radius: 24,
      animation_style: "pulse",
      monitor_target: "primary",
      color: "#6366f1",
    },
    theme: DEFAULT_THEME,
  });

  // Notification Storage State
  let notifications = $state<Notification[]>([]);
  let isNotificationsLoading = $state(true);
  let isClearing = $state(false);
  let showClearConfirm = $state(false);

  // Glow Preview & Sound State
  let isPreviewingGlow = $state(false);
  let isPlayingSound = $state(false);

  // Pipeline Status State
  let pipelineStatus = $state<PipelineStatus | null>(null);
  let isSendingTest = $state(false);
  let testNotificationError = $state<string | null>(null);

  // Derived Computed Values
  let statusInfo = $derived(getStatusLabel(pipelineStatus?.provider_status));
  let unreadCount = $derived(notifications.filter((n) => !n.read).length);
  let criticalCount = $derived(notifications.filter((n) => n.urgency === "critical").length);
  let highCount = $derived(notifications.filter((n) => n.urgency === "high").length);
  let filteredNotifications = $derived.by(() => {
    if (feedFilter === "unread") return notifications.filter((n) => !n.read);
    if (feedFilter === "critical") return notifications.filter((n) => n.urgency === "critical" || n.urgency === "high");
    return notifications;
  });
  let recentNotifications = $derived(notifications.slice(0, 4));

  function getStatusLabel(st: ProviderStatus | undefined): { label: string; kind: "ok" | "warn" | "err" } {
    if (!st) return { label: "Unknown", kind: "warn" };
    if (typeof st === "string") {
      switch (st) {
        case "listening":
          return { label: "Listening", kind: "ok" };
        case "permission_required":
          return { label: "Permission Required", kind: "warn" };
        case "permission_denied":
          return { label: "Permission Denied", kind: "err" };
        case "unsupported":
          return { label: "Unsupported OS", kind: "err" };
        case "idle":
          return { label: "Idle", kind: "warn" };
        default:
          return { label: st, kind: "warn" };
      }
    }
    if ("error" in st) {
      return { label: `Error: ${st.error}`, kind: "err" };
    }
    return { label: "Unknown", kind: "warn" };
  }

  async function checkConnection() {
    isPinging = true;
    errorMessage = null;

    try {
      const info = await invoke<ConnectionInfo>("check_backend_connection");
      connectionInfo = info;
      status = "connected";
      lastCheckedAt = new Date().toLocaleTimeString();
      pingCount += 1;

      await fetchAppSettings();
      await fetchNotifications();
      await fetchPipelineStatus();
    } catch (err: unknown) {
      status = "disconnected";
      errorMessage = err instanceof Error ? err.message : String(err);
      lastCheckedAt = new Date().toLocaleTimeString();
    } finally {
      isPinging = false;
    }
  }

  let isSettingsLoading = $state(true);
  let settingsError = $state<string | null>(null);
  let settingsErrorTimer: ReturnType<typeof setTimeout> | null = null;
  let saveDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  async function fetchAppSettings() {
    try {
      const s = await invoke<AppSettings>("get_app_settings");
      appSettings = s;
      isEnabled = s.enabled;
      if (s.theme && THEMES.some((t) => t.id === s.theme)) {
        selectedTheme = s.theme;
      }
    } catch (err) {
      console.error("Failed to fetch app settings:", err);
    } finally {
      isSettingsLoading = false;
    }
  }

  function saveAppSettingsDebounced(ms = 150) {
    if (saveDebounceTimer) clearTimeout(saveDebounceTimer);
    saveDebounceTimer = setTimeout(() => {
      saveAppSettings();
    }, ms);
  }

  async function saveAppSettings() {
    if (saveDebounceTimer) clearTimeout(saveDebounceTimer);
    try {
      appSettings.enabled = isEnabled;
      const updated = await invoke<AppSettings>("update_app_settings", { settings: appSettings });
      appSettings = updated;
      isEnabled = updated.enabled;
      settingsError = null;
    } catch (err: unknown) {
      console.error("Failed to save app settings:", err);
      settingsError = err instanceof Error ? err.message : String(err);
      if (settingsErrorTimer) clearTimeout(settingsErrorTimer);
      settingsErrorTimer = setTimeout(() => {
        settingsError = null;
      }, 5000);
      await fetchAppSettings();
    }
  }

  async function toggleAppState() {
    isToggling = true;
    try {
      const next = await invoke<boolean>("toggle_app_state");
      isEnabled = next;
      appSettings.enabled = next;
      await fetchPipelineStatus();
    } catch (err) {
      console.error("Failed to toggle app state:", err);
    } finally {
      isToggling = false;
    }
  }

  async function toggleStartup() {
    try {
      const target = !appSettings.startup_enabled;
      const res = await invoke<boolean>("set_startup_status", { enabled: target });
      appSettings.startup_enabled = res;
    } catch (err) {
      console.error("Failed to toggle startup:", err);
    }
  }

  async function testSoundAlert() {
    isPlayingSound = true;
    try {
      await invoke("play_sound_preview");
    } catch (err) {
      console.error("Failed to play sound preview:", err);
    } finally {
      setTimeout(() => {
        isPlayingSound = false;
      }, 400);
    }
  }

  async function fetchPipelineStatus() {
    try {
      pipelineStatus = await invoke<PipelineStatus>("get_pipeline_status");
    } catch (err) {
      console.error("Failed to fetch pipeline status:", err);
    }
  }

  async function fetchNotifications() {
    try {
      notifications = await invoke<Notification[]>("get_notifications");
    } catch (err) {
      console.error("Failed to fetch stored notifications:", err);
    } finally {
      isNotificationsLoading = false;
    }
  }

  async function previewGlow() {
    isPreviewingGlow = true;
    try {
      await saveAppSettings();
      await invoke("trigger_glow_preview");
    } catch (err) {
      console.error("Failed to trigger glow preview:", err);
    } finally {
      setTimeout(() => {
        isPreviewingGlow = false;
      }, 1000);
    }
  }

  async function confirmClearAll() {
    isClearing = true;
    try {
      await invoke("clear_notifications");
      notifications = [];
      showClearConfirm = false;
    } catch (err) {
      console.error("Failed to clear notifications:", err);
    } finally {
      isClearing = false;
    }
  }

  async function removeNotification(id: string) {
    try {
      await invoke("dismiss_notification", { id });
      notifications = notifications.filter((n) => n.id !== id);
    } catch (err) {
      console.error(`Failed to remove notification ${id}:`, err);
    }
  }

  async function markAsRead(id: string) {
    try {
      await invoke("mark_notification_as_read", { id });
      notifications = notifications.map((n) => (n.id === id ? { ...n, read: true } : n));
    } catch (err) {
      console.error(`Failed to mark notification ${id} as read:`, err);
    }
  }

  async function markAsUnread(id: string) {
    try {
      await invoke("mark_notification_as_unread", { id });
      notifications = notifications.map((n) => (n.id === id ? { ...n, read: false } : n));
    } catch (err) {
      console.error(`Failed to mark notification ${id} as unread:`, err);
    }
  }

  async function sendTestNotification() {
    isSendingTest = true;
    testNotificationError = null;
    try {
      const notif = await invoke<Notification>("send_test_notification");
      if (!notifications.some((n) => n.id === notif.id)) {
        notifications = [notif, ...notifications];
      }
      await fetchPipelineStatus();
    } catch (err: unknown) {
      testNotificationError = err instanceof Error ? err.message : String(err);
    } finally {
      isSendingTest = false;
    }
  }

  async function openWindowsSettings() {
    try {
      await invoke("open_notification_settings");
    } catch (err) {
      console.error("Failed to open Windows notification settings:", err);
    }
  }

  onMount(() => {
    try {
      // [LEGACY / BACKWARDS COMPATIBILITY] Read curry_selected_theme with fallback to legacy notiglow_selected_theme
      const savedTheme = (localStorage.getItem("curry_selected_theme") || localStorage.getItem("notiglow_selected_theme")) as ThemeId;
      if (savedTheme && THEMES.some((t) => t.id === savedTheme)) {
        selectedTheme = savedTheme;
      } else {
        selectedTheme = DEFAULT_THEME;
      }
    } catch {
      selectedTheme = DEFAULT_THEME;
    }

    checkConnection();
    fetchAppSettings();
    fetchPipelineStatus();
    fetchNotifications();

    const unlistenStatePromise = listen<boolean>("app-state-changed", (event) => {
      isEnabled = event.payload;
      appSettings.enabled = event.payload;
      fetchPipelineStatus();
    });

    const unlistenSettingsPromise = listen<AppSettings>("app-settings-updated", (event) => {
      appSettings = event.payload;
      isEnabled = event.payload.enabled;
      if (event.payload.theme && THEMES.some((t) => t.id === event.payload.theme)) {
        selectedTheme = event.payload.theme;
      }
    });

    const unlistenTraySettingsPromise = listen<void>("open-settings-tab", () => {
      navigateToTab("settings");
    });

    const handleNewNotif = (notif: Notification) => {
      testNotificationError = null;
      notifications = [notif, ...notifications.filter((n) => n.id !== notif.id)];
      fetchPipelineStatus();
    };

    const unlistenNotifPromise = listen<Notification>("notification-received", (event) => {
      handleNewNotif(event.payload);
    });

    const unlistenCreatedPromise = listen<Notification>("notification-created", (event) => {
      handleNewNotif(event.payload);
    });

    const unlistenClearedPromise = listen<void>("notifications-cleared", () => {
      notifications = [];
    });

    const unlistenRemovedPromise = listen<string>("notification-removed", (event) => {
      const removedId = event.payload;
      notifications = notifications.filter((n) => n.id !== removedId);
    });

    const unlistenReadPromise = listen<string>("notification-read-updated", (event) => {
      const readId = event.payload;
      notifications = notifications.map((n) => (n.id === readId ? { ...n, read: true } : n));
    });

    const unlistenUnreadPromise = listen<string>("notification-unread-updated", (event) => {
      const unreadId = event.payload;
      notifications = notifications.map((n) => (n.id === unreadId ? { ...n, read: false } : n));
    });

    const unlistenReadStatusPromise = listen<[string, boolean]>("notification-read-status-changed", (event) => {
      const [statusId, isRead] = event.payload;
      notifications = notifications.map((n) => (n.id === statusId ? { ...n, read: isRead } : n));
    });

    const interval = setInterval(() => {
      fetchPipelineStatus();
    }, 4000);

    return () => {
      clearInterval(interval);
      unlistenStatePromise.then((unlisten) => unlisten());
      unlistenSettingsPromise.then((unlisten) => unlisten());
      unlistenTraySettingsPromise.then((unlisten) => unlisten());
      unlistenNotifPromise.then((unlisten) => unlisten());
      unlistenCreatedPromise.then((unlisten) => unlisten());
      unlistenClearedPromise.then((unlisten) => unlisten());
      unlistenRemovedPromise.then((unlisten) => unlisten());
      unlistenReadPromise.then((unlisten) => unlisten());
      unlistenUnreadPromise.then((unlisten) => unlisten());
      unlistenReadStatusPromise.then((unlisten) => unlisten());
    };
  });
</script>

<svelte:window
  onkeydown={(e) => {
    if (e.key === "Escape") {
      if (showClearConfirm) {
        showClearConfirm = false;
      }
      if (settingsError) {
        settingsError = null;
      }
      if (testNotificationError) {
        testNotificationError = null;
      }
      if (errorMessage) {
        errorMessage = null;
      }
    }
  }}
/>

<div class="shell" data-theme={selectedTheme}>
  <div class="ambient-glow-mesh" aria-hidden="true"></div>

  <!-- Windows 11 Desktop Top Header (Brand + Top Nav Tabs + Authoritative Listening Status) -->
  <header class="app-header">
    <div class="header-left">
      <div class="brand-badge" title="Curry Desktop">
        <img src="/favicon.png" alt="Curry" class="brand-icon-img" />
      </div>
      <div class="brand-text">
        <div class="brand-row">
          <h1 class="brand-title">Curry</h1>
          <span class="brand-version-pill">v{connectionInfo?.version ?? '1.0.0'}</span>
        </div>
      </div>
    </div>

    <!-- Center Top Navigation Tabs -->
    <nav class="header-center-tabs" aria-label="Main Navigation">
      <button
        id="tab-dashboard-btn"
        class="nav-tab {activeTab === 'dashboard' ? 'active' : ''}"
        onclick={() => navigateToTab("dashboard")}
        onkeydown={(e) => handleTabKeyDown(e, "dashboard")}
        aria-current={activeTab === 'dashboard' ? 'page' : undefined}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="nav-icon">
          <rect x="3" y="3" width="7" height="7" rx="1.5"></rect>
          <rect x="14" y="3" width="7" height="7" rx="1.5"></rect>
          <rect x="14" y="14" width="7" height="7" rx="1.5"></rect>
          <rect x="3" y="14" width="7" height="7" rx="1.5"></rect>
        </svg>
        <span>Dashboard</span>
      </button>

      <button
        id="tab-notifications-btn"
        class="nav-tab {activeTab === 'notifications' ? 'active' : ''}"
        onclick={() => navigateToTab("notifications")}
        onkeydown={(e) => handleTabKeyDown(e, "notifications")}
        aria-current={activeTab === 'notifications' ? 'page' : undefined}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="nav-icon">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
        </svg>
        <span>Notifications</span>
        {#if unreadCount > 0}
          <span class="nav-badge-pill">{unreadCount}</span>
        {/if}
      </button>

      <button
        id="tab-glow-btn"
        class="nav-tab {activeTab === 'glow' ? 'active' : ''}"
        onclick={() => navigateToTab("glow")}
        onkeydown={(e) => handleTabKeyDown(e, "glow")}
        aria-current={activeTab === 'glow' ? 'page' : undefined}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="nav-icon">
          <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon>
        </svg>
        <span>Glow</span>
      </button>

      <button
        id="tab-settings-btn"
        class="nav-tab {activeTab === 'settings' ? 'active' : ''}"
        onclick={() => navigateToTab("settings")}
        onkeydown={(e) => handleTabKeyDown(e, "settings")}
        aria-current={activeTab === 'settings' ? 'page' : undefined}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="nav-icon">
          <circle cx="12" cy="12" r="3"></circle>
          <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
        </svg>
        <span>Settings</span>
      </button>
    </nav>

    <!-- Right: Interactive Listening Status Button (Authoritative Listener State) -->
    <div class="header-right">
      <button
        id="toggle-state-btn"
        class="listening-toggle-btn {isEnabled ? statusInfo.kind : 'paused'}"
        onclick={toggleAppState}
        disabled={isToggling}
        role="switch"
        aria-checked={isEnabled}
        title={isEnabled ? "Notification listener active. Click to pause." : "Notification listener paused. Click to resume."}
        aria-label={isEnabled ? "Notification listener active. Click to pause." : "Notification listener paused. Click to resume."}
      >
        <span class="status-dot-pulse {isEnabled ? statusInfo.kind : 'paused'}"></span>
        <span class="status-text-label">
          {#if !isEnabled}
            Paused
          {:else}
            {statusInfo.label}
          {/if}
        </span>
      </button>
    </div>
  </header>

  <!-- Main Viewport with Directional Hero Page Transition -->
  <main class="main-viewport" id="main-content">
    {#key activeTab}
      <div class="hero-page-container page-{navDirection}">
        {#if activeTab === "dashboard"}
          <!-- ============================================================= -->
          <!-- DASHBOARD VIEW                                                -->
          <!-- ============================================================= -->
          <div class="view-container dashboard-view">
            <div class="view-hero-header">
              <span class="hero-eyebrow">Windows 11 Desktop Engine</span>
              <h2 class="view-title">Dashboard Overview</h2>
              <p class="view-subtitle">Real-time status of your Windows notification listener and ambient glow engine.</p>
            </div>

            <!-- 4 Metric Cards (Using actual live data) -->
            <div class="metrics-grid">
              <div class="metric-card">
                <div class="metric-top">
                  <span class="metric-label">Total Captured</span>
                  <div class="metric-icon-wrap total">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <rect x="2" y="3" width="20" height="14" rx="2"></rect>
                      <line x1="8" y1="21" x2="16" y2="21"></line>
                      <line x1="12" y1="17" x2="12" y2="21"></line>
                    </svg>
                  </div>
                </div>
                <div class="metric-value">{notifications.length}</div>
                <span class="metric-foot">Stored in local history</span>
              </div>

              <div class="metric-card">
                <div class="metric-top">
                  <span class="metric-label">Unread Alerts</span>
                  <div class="metric-icon-wrap unread">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
                      <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
                    </svg>
                  </div>
                </div>
                <div class="metric-value">{unreadCount}</div>
                <span class="metric-foot">Awaiting user review</span>
              </div>

              <div class="metric-card">
                <div class="metric-top">
                  <span class="metric-label">Critical Alerts</span>
                  <div class="metric-icon-wrap critical">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
                      <line x1="12" y1="9" x2="12" y2="13"></line>
                      <line x1="12" y1="17" x2="12.01" y2="17"></line>
                    </svg>
                  </div>
                </div>
                <div class="metric-value">{criticalCount}</div>
                <span class="metric-foot">High urgency visual glow</span>
              </div>

              <div class="metric-card">
                <div class="metric-top">
                  <span class="metric-label">High Priority</span>
                  <div class="metric-icon-wrap high">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon>
                    </svg>
                  </div>
                </div>
                <div class="metric-value">{highCount}</div>
                <span class="metric-foot">Priority alert level</span>
              </div>
            </div>

            <!-- System Diagnostics & Pipeline Status -->
            <div class="dashboard-section-row">
              <div class="card panel-card system-status-panel">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M22 12h-4l-3 9L9 3l-3 9H2"></path>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">System & Listener Diagnostics</h3>
                      <p class="panel-desc">Tauri backend health and Windows UserNotificationListener pipeline</p>
                    </div>
                  </div>

                  <div class="panel-header-actions">
                    <button
                      class="secondary-btn"
                      onclick={sendTestNotification}
                      disabled={isSendingTest}
                      title="Send test desktop notification toast"
                    >
                      <svg class="btn-icon {isSendingTest ? 'spin' : ''}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="22" y1="2" x2="11" y2="13"></line>
                        <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
                      </svg>
                      <span>{isSendingTest ? "Sending..." : "Test Toast"}</span>
                    </button>

                    <button
                      id="ping-btn"
                      class="secondary-btn"
                      onclick={checkConnection}
                      disabled={isPinging}
                      aria-label="Ping backend"
                    >
                      <svg class="btn-icon {isPinging ? 'spin' : ''}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="23 4 23 10 17 10"></polyline>
                        <polyline points="1 20 1 14 7 14"></polyline>
                        <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
                      </svg>
                      <span>{isPinging ? "Checking..." : "Test IPC"}</span>
                    </button>
                  </div>
                </div>

                {#if connectionInfo}
                  <div class="system-meta-grid">
                    <div class="meta-box">
                      <span class="meta-box-label">Application</span>
                      <span class="meta-box-val">{connectionInfo.app_name} v{connectionInfo.version}</span>
                    </div>
                    <div class="meta-box">
                      <span class="meta-box-label">Target OS & Arch</span>
                      <span class="meta-box-val">{connectionInfo.target_os} ({connectionInfo.target_arch})</span>
                    </div>
                    <div class="meta-box">
                      <span class="meta-box-label">Notification Provider</span>
                      <span class="meta-box-val">{pipelineStatus?.provider_name ?? "Windows Native"}</span>
                    </div>
                    <div class="meta-box">
                      <span class="meta-box-label">Listener Pipeline</span>
                      <span class="status-chip {statusInfo.kind}">
                        {statusInfo.label}
                      </span>
                    </div>
                  </div>
                {/if}

                {#if pipelineStatus?.provider_status === "permission_required" || pipelineStatus?.provider_status === "permission_denied"}
                  <div class="permission-warning-banner">
                    <span>Windows Notification Listener permissions are required to capture incoming alerts.</span>
                    <button class="settings-link-btn" onclick={openWindowsSettings}>
                      Open Windows Settings
                    </button>
                  </div>
                {/if}
              </div>
            </div>

            <!-- Recent Notifications Strip -->
            <div class="dashboard-section-row">
              <div class="card panel-card recent-feed-panel">
                <div class="panel-header">
                  <div>
                    <h3 class="panel-title">Recent Notifications</h3>
                    <p class="panel-desc">Latest desktop alerts captured by Curry</p>
                  </div>
                  {#if notifications.length > 0}
                    <button class="text-link-btn" onclick={() => navigateToTab("notifications")}>
                      View All ({notifications.length}) &rarr;
                    </button>
                  {/if}
                </div>

                {#if notifications.length === 0}
                  <div class="empty-state-panel">
                    <div class="empty-icon-ring">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
                        <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
                      </svg>
                    </div>
                    <h4 class="empty-heading">No notifications captured yet</h4>
                    <p class="empty-paragraph">Windows desktop alerts captured by Curry will appear here in real time.</p>
                    <button class="primary-btn" onclick={sendTestNotification} disabled={isSendingTest}>
                      Send Sample Alert
                    </button>
                  </div>
                {:else}
                  <div class="recent-list">
                    {#each recentNotifications as item (item.id)}
                      <div class="recent-item-row {item.read ? 'is-read' : 'is-unread'} urgency-{item.urgency ?? 'normal'}">
                        <div class="recent-avatar">
                          {#if item.icon}
                            <img src={item.icon} alt={item.app_name} class="recent-avatar-img" />
                          {:else}
                            <span>{getAppInitial(item.app_name || item.source_app)}</span>
                          {/if}
                        </div>

                        <div class="recent-content">
                          <div class="recent-top-line">
                            <span class="recent-source">{item.app_name || item.source_app || "Unknown"}</span>
                            {#if item.urgency}
                              <span class="urgency-tag {item.urgency}">{item.urgency}</span>
                            {/if}
                            <span class="recent-time">{formatTimestamp(item.timestamp)}</span>
                          </div>
                          <div class="recent-title">{item.title || "(No Title)"}</div>
                          <div class="recent-body">{item.body || item.message || ""}</div>
                        </div>

                        <div class="recent-actions">
                          {#if !item.read}
                            <button class="tiny-action-btn" onclick={() => markAsRead(item.id)} title="Mark Read">
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="20 6 9 17 4 12"></polyline>
                              </svg>
                            </button>
                          {/if}
                          <button class="tiny-action-btn danger" onclick={() => removeNotification(item.id)} title="Dismiss">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                              <line x1="18" y1="6" x2="6" y2="18"></line>
                              <line x1="6" y1="6" x2="18" y2="18"></line>
                            </svg>
                          </button>
                        </div>
                      </div>
                    {/each}
                  </div>
                {/if}
              </div>
            </div>
          </div>

        {:else if activeTab === "notifications"}
          <!-- ============================================================= -->
          <!-- NOTIFICATIONS VIEW                                            -->
          <!-- ============================================================= -->
          <div class="view-container notifications-view">
            <div class="view-hero-header view-header-row">
              <div>
                <span class="hero-eyebrow">Local Archive</span>
                <h2 class="view-title">Notification Feed</h2>
                <p class="view-subtitle">Captured Windows toast alerts, local history, and filter tools</p>
              </div>

              <div class="feed-header-controls">
                <!-- Filter Segmented Tabs -->
                <div class="segmented-control" role="radiogroup" aria-label="Filter notifications">
                  <button
                    class="segment-btn {feedFilter === 'all' ? 'active' : ''}"
                    onclick={() => (feedFilter = "all")}
                  >
                    All ({notifications.length})
                  </button>
                  <button
                    class="segment-btn {feedFilter === 'unread' ? 'active' : ''}"
                    onclick={() => (feedFilter = "unread")}
                  >
                    Unread ({unreadCount})
                  </button>
                  <button
                    class="segment-btn {feedFilter === 'critical' ? 'active' : ''}"
                    onclick={() => (feedFilter = "critical")}
                  >
                    Priority ({criticalCount + highCount})
                  </button>
                </div>

                <!-- Clear All Button -->
                <button
                  id="clear-all-btn"
                  class="danger-btn"
                  onclick={() => (showClearConfirm = true)}
                  disabled={notifications.length === 0 || isClearing}
                  aria-label="Clear all notifications"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="btn-icon">
                    <polyline points="3 6 5 6 21 6"></polyline>
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                  </svg>
                  <span>Clear All</span>
                </button>
              </div>
            </div>

            {#if testNotificationError}
              <div class="alert-banner danger">
                <span>{testNotificationError}</span>
                <button class="banner-close" onclick={() => (testNotificationError = null)}>✕</button>
              </div>
            {/if}

            <!-- Notification Cards List -->
            <div class="notification-feed-list" role="feed" aria-label="Notification list">
              {#if isNotificationsLoading}
                <div class="empty-state-panel">
                  <div class="empty-icon-ring spin">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                      <circle cx="12" cy="12" r="9" stroke-dasharray="14 14"></circle>
                    </svg>
                  </div>
                  <h4 class="empty-heading">Loading local history...</h4>
                  <p class="empty-paragraph">Retrieving persisted records from disk.</p>
                </div>
              {:else if filteredNotifications.length === 0}
                <div class="empty-state-panel">
                  <div class="empty-icon-ring">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
                      <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
                    </svg>
                  </div>
                  <h4 class="empty-heading">
                    {feedFilter === "unread"
                      ? "No unread notifications"
                      : feedFilter === "critical"
                      ? "No priority notifications"
                      : "No notifications in history"}
                  </h4>
                  <p class="empty-paragraph">
                    {feedFilter === "unread"
                      ? "You are all caught up! All captured alerts are marked as read."
                      : feedFilter === "critical"
                      ? "No critical or high-priority notifications have been recorded."
                      : "Notifications captured from Windows will automatically appear here."}
                  </p>
                  {#if feedFilter === "all"}
                    <button class="primary-btn" onclick={sendTestNotification} disabled={isSendingTest}>
                      Send Sample Alert
                    </button>
                  {/if}
                </div>
              {:else}
                <div class="cards-stack">
                  {#each filteredNotifications as item (item.id)}
                    <article class="notification-card {item.read ? 'is-read' : 'is-unread'} urgency-{item.urgency ?? 'normal'}">
                      <div class="card-avatar-col">
                        <div class="card-avatar" title={item.app_name || item.source_app || "Application"}>
                          {#if item.icon}
                            <img src={item.icon} alt={item.app_name} class="avatar-img" />
                          {:else}
                            <span class="avatar-initial">{getAppInitial(item.app_name || item.source_app)}</span>
                          {/if}
                        </div>
                        {#if !item.read}
                          <span class="card-unread-dot" title="Unread"></span>
                        {/if}
                      </div>

                      <div class="card-body-col">
                        <div class="card-meta-line">
                          <div class="card-source-tags">
                            <span class="card-source-name">{item.app_name || item.source_app || "Unknown Application"}</span>
                            <span class="platform-chip">{item.platform}</span>
                            {#if item.urgency}
                              <span class="urgency-chip {item.urgency}">{item.urgency}</span>
                            {/if}
                          </div>
                          <time class="card-timestamp" datetime={new Date(item.timestamp).toISOString()}>
                            {formatTimestamp(item.timestamp)}
                          </time>
                        </div>

                        <h4 class="card-title">{item.title || "(No Title)"}</h4>
                        <p class="card-text">{item.body || item.message || "(No message body)"}</p>

                        <div class="card-footer-line">
                          <span class="card-id-code">ID: {item.id}</span>
                          <div class="card-actions-group">
                            {#if !item.read}
                              <button
                                class="item-action-btn read"
                                onclick={() => markAsRead(item.id)}
                                aria-label="Mark notification as read"
                              >
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="tiny-icon">
                                  <polyline points="20 6 9 17 4 12"></polyline>
                                </svg>
                                <span>Mark Read</span>
                              </button>
                            {:else}
                              <button
                                class="item-action-btn unread"
                                onclick={() => markAsUnread(item.id)}
                                aria-label="Mark notification as unread"
                              >
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="tiny-icon">
                                  <circle cx="12" cy="12" r="4"></circle>
                                </svg>
                                <span>Mark Unread</span>
                              </button>
                            {/if}

                            <button
                              class="item-action-btn dismiss"
                              onclick={() => removeNotification(item.id)}
                              aria-label="Dismiss notification"
                            >
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="tiny-icon">
                                <line x1="18" y1="6" x2="6" y2="18"></line>
                                <line x1="6" y1="6" x2="18" y2="18"></line>
                              </svg>
                              <span>Dismiss</span>
                            </button>
                          </div>
                        </div>
                      </div>
                    </article>
                  {/each}
                </div>
              {/if}
            </div>
          </div>

        {:else if activeTab === "glow"}
          <!-- ============================================================= -->
          <!-- GLOW EXPERIENCE VIEW                                          -->
          <!-- ============================================================= -->
          <div class="view-container glow-view">
            <div class="view-hero-header view-header-row">
              <div>
                <span class="hero-eyebrow">Visual Illumination</span>
                <h2 class="view-title">Screen-Edge Glow Experience</h2>
                <p class="view-subtitle">Ambient border illumination triggered during incoming desktop notifications</p>
              </div>

              <button
                id="preview-glow-btn"
                class="primary-btn"
                onclick={previewGlow}
                disabled={isPreviewingGlow || !appSettings.glow.enabled}
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="btn-icon {isPreviewingGlow ? 'pulse' : ''}">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                  <circle cx="12" cy="12" r="3"></circle>
                </svg>
                <span>{isPreviewingGlow ? "Previewing..." : "Preview Glow"}</span>
              </button>
            </div>

            <div class="settings-sections-stack">
              <!-- Master Toggle & Animation Dynamic -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <polygon points="10 8 16 12 10 16 10 8"></polygon>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Glow Activation & Style</h3>
                      <p class="panel-desc">Enable edge illumination and choose the visual motion style</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Enable Screen-Edge Glow</span>
                      <span class="control-sub">Render ambient edge glow on incoming desktop notifications</span>
                    </div>
                    <button
                      class="switch-control {appSettings.glow.enabled ? 'on' : 'off'}"
                      onclick={() => {
                        appSettings.glow.enabled = !appSettings.glow.enabled;
                        saveAppSettings();
                      }}
                      role="switch"
                      aria-checked={appSettings.glow.enabled}
                      aria-label="Toggle screen-edge glow"
                    >
                      <span class="switch-ball"></span>
                    </button>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Animation Dynamic</span>
                      <span class="control-sub">Wave dynamic used during notification display</span>
                    </div>
                    <select
                      bind:value={appSettings.glow.animation_style}
                      onchange={saveAppSettings}
                      class="native-select"
                    >
                      <option value="pulse">Pulse (Periodic rhythmic pulse)</option>
                      <option value="breathing">Breathing (Slow organic swell)</option>
                      <option value="solid">Solid (Stationary static border)</option>
                    </select>
                  </div>
                </div>
              </section>

              <!-- Geometry & Curvature -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="3" y="3" width="18" height="18" rx="4"></rect>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Edge Geometry & Sizing</h3>
                      <p class="panel-desc">Configure screen border outline thickness and corner curvature</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Border Thickness</span>
                      <span class="control-sub">Outline width ({appSettings.glow.thickness} px)</span>
                    </div>
                    <div class="slider-box">
                      <input
                        type="range"
                        min="2"
                        max="32"
                        step="1"
                        bind:value={appSettings.glow.thickness}
                        oninput={() => saveAppSettingsDebounced(150)}
                        onchange={saveAppSettings}
                        class="native-slider"
                      />
                      <span class="slider-val-badge">{appSettings.glow.thickness}px</span>
                    </div>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Corner Rounding</span>
                      <span class="control-sub">Screen edge curvature radius ({appSettings.glow.corner_radius} px)</span>
                    </div>
                    <div class="slider-box">
                      <input
                        type="range"
                        min="0"
                        max="48"
                        step="2"
                        bind:value={appSettings.glow.corner_radius}
                        oninput={() => saveAppSettingsDebounced(150)}
                        onchange={saveAppSettings}
                        class="native-slider"
                      />
                      <span class="slider-val-badge">{appSettings.glow.corner_radius}px</span>
                    </div>
                  </div>
                </div>
              </section>

              <!-- Duration & Intensity -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <polyline points="12 6 12 12 16 14"></polyline>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Timing & Peak Intensity</h3>
                      <p class="panel-desc">Length of notification illumination and peak brightness level</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Glow Duration</span>
                      <span class="control-sub">Display length ({appSettings.glow.duration_ms} ms)</span>
                    </div>
                    <div class="slider-box">
                      <input
                        type="range"
                        min="500"
                        max="10000"
                        step="250"
                        bind:value={appSettings.glow.duration_ms}
                        oninput={() => saveAppSettingsDebounced(150)}
                        onchange={saveAppSettings}
                        class="native-slider"
                      />
                      <span class="slider-val-badge">{appSettings.glow.duration_ms}ms</span>
                    </div>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Peak Opacity</span>
                      <span class="control-sub">Maximum brightness ({Math.round(appSettings.glow.intensity * 100)}%)</span>
                    </div>
                    <div class="slider-box">
                      <input
                        type="range"
                        min="0.1"
                        max="1.0"
                        step="0.05"
                        bind:value={appSettings.glow.intensity}
                        oninput={() => saveAppSettingsDebounced(150)}
                        onchange={saveAppSettings}
                        class="native-slider"
                      />
                      <span class="slider-val-badge">{Math.round(appSettings.glow.intensity * 100)}%</span>
                    </div>
                  </div>
                </div>
              </section>

              <!-- Multi-Monitor Targeting & Accent Color -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="2" y="3" width="20" height="14" rx="2"></rect>
                        <line x1="8" y1="21" x2="16" y2="21"></line>
                        <line x1="12" y1="17" x2="12" y2="21"></line>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Display Targeting & Color</h3>
                      <p class="panel-desc">Select screen target and default notification illumination hue</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Monitor Target</span>
                      <span class="control-sub">Display monitor where the glow overlay is rendered</span>
                    </div>
                    <select
                      bind:value={appSettings.glow.monitor_target}
                      onchange={saveAppSettings}
                      class="native-select"
                    >
                      <option value="primary">Primary Display</option>
                      <option value="active">Active Window Display</option>
                      <option value="all">All Displays (Multi-Monitor)</option>
                    </select>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Default Glow Color</span>
                      <span class="control-sub">Standard hue (Urgency overrides: Critical = Red, High = Amber)</span>
                    </div>
                    <div class="color-palette-wrap">
                      {#each currentGlowPalette as col}
                        <button
                          class="color-dot {appSettings.glow.color.toLowerCase() === col.toLowerCase() ? 'selected' : ''}"
                          style:background-color={col}
                          onclick={() => {
                            appSettings.glow.color = col;
                            saveAppSettings();
                          }}
                          title="Select preset {col}"
                          aria-label="Select preset color {col}"
                        ></button>
                      {/each}

                      <div class="color-divider" aria-hidden="true"></div>

                      <div class="custom-color-control" title="Choose custom HEX color">
                        <label class="custom-color-picker-label" aria-label="Open color palette picker">
                          <svg class="palette-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="13.5" cy="6.5" r=".5" fill="currentColor"></circle>
                            <circle cx="17.5" cy="10.5" r=".5" fill="currentColor"></circle>
                            <circle cx="8.5" cy="7.5" r=".5" fill="currentColor"></circle>
                            <circle cx="6.5" cy="12.5" r=".5" fill="currentColor"></circle>
                            <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 9.5 10c.93 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.24-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c3.31 0 6-2.69 6-6 0-5.5-4.5-10-10-10z"></path>
                          </svg>
                          <input
                            type="color"
                            bind:value={appSettings.glow.color}
                            onchange={saveAppSettings}
                            class="custom-color-input-hidden"
                            aria-label="Choose custom hex color"
                          />
                        </label>
                        <span class="custom-color-hex-tag">{appSettings.glow.color.toUpperCase()}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </section>
            </div>
          </div>

        {:else}
          <!-- ============================================================= -->
          <!-- SETTINGS VIEW                                                 -->
          <!-- ============================================================= -->
          <div class="view-container settings-view">
            <div class="view-hero-header">
              <span class="hero-eyebrow">Configuration & Personalization</span>
              <h2 class="view-title">Application Settings</h2>
              <p class="view-subtitle">Appearance, themes, Windows autostart, sound alerts, and retention policies</p>
            </div>

            {#if settingsError}
              <div class="alert-banner danger">
                <span>Settings save error: {settingsError}</span>
                <button class="banner-close" onclick={() => (settingsError = null)}>✕</button>
              </div>
            {/if}

            <div class="settings-sections-stack">
              <!-- Appearance & 7 Themes -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <path d="M12 2a10 10 0 0 0 0 20"></path>
                        <path d="M12 2v20"></path>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Appearance & Themes</h3>
                      <p class="panel-desc">Select from 7 curated design systems (Default: Perpetuity)</p>
                    </div>
                  </div>
                </div>

                <div class="themes-grid" role="radiogroup" aria-label="Visual Themes">
                  {#each THEMES as theme}
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selectedTheme === theme.id}
                      class="theme-picker-card {selectedTheme === theme.id ? 'active' : ''}"
                      onclick={() => selectTheme(theme.id)}
                      id="theme-{theme.id}-btn"
                    >
                      <div class="theme-card-top">
                        <span class="theme-card-title">{theme.label}</span>
                        {#if selectedTheme === theme.id}
                          <span class="theme-selected-pill">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" class="tiny-icon">
                              <polyline points="20 6 9 17 4 12"></polyline>
                            </svg>
                            Active
                          </span>
                        {/if}
                      </div>

                      <p class="theme-card-desc">{theme.description}</p>

                      <!-- Swatches Row -->
                      <div class="theme-swatches-strip" aria-hidden="true">
                        {#each theme.swatches as swatch}
                          <span class="theme-swatch-circle" style="background-color: {swatch};"></span>
                        {/each}
                      </div>

                      <!-- Mini UI Preview Box -->
                      <div
                        class="theme-mini-frame"
                        style="background-color: {theme.colors.background}; border-color: {theme.colors.border};"
                        aria-hidden="true"
                      >
                        <div class="mini-frame-header">
                          <span class="mini-frame-accent-dot" style="background-color: {theme.colors.accent};"></span>
                          <span class="mini-frame-bar" style="background-color: {theme.colors.accent}; width: 45%;"></span>
                        </div>
                        <span class="mini-frame-bar secondary" style="background-color: {theme.colors.text}; width: 80%;"></span>
                      </div>
                    </button>
                  {/each}
                </div>
              </section>

              <!-- Windows Startup & Behavior -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="2" y="3" width="20" height="14" rx="2"></rect>
                        <line x1="8" y1="21" x2="16" y2="21"></line>
                        <line x1="12" y1="17" x2="12" y2="21"></line>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Windows Integration & Startup</h3>
                      <p class="panel-desc">Desktop lifecycle, background tray behavior, and startup toggles</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Start with Windows</span>
                      <span class="control-sub">Launch Curry automatically in the system tray upon Windows login</span>
                    </div>
                    <button
                      class="switch-control {appSettings.startup_enabled ? 'on' : 'off'}"
                      onclick={toggleStartup}
                      role="switch"
                      aria-checked={appSettings.startup_enabled}
                      aria-label="Toggle start with Windows"
                    >
                      <span class="switch-ball"></span>
                    </button>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Master Monitoring Switch</span>
                      <span class="control-sub">Global kill-switch for notification capture and edge illumination</span>
                    </div>
                    <button
                      class="switch-control {appSettings.enabled ? 'on' : 'off'}"
                      onclick={() => {
                        appSettings.enabled = !appSettings.enabled;
                        saveAppSettings();
                      }}
                      role="switch"
                      aria-checked={appSettings.enabled}
                      aria-label="Toggle master monitoring switch"
                    >
                      <span class="switch-ball"></span>
                    </button>
                  </div>
                </div>
              </section>

              <!-- Sound Alerts -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"></polygon>
                        <path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"></path>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">Audio Chime Alerts</h3>
                      <p class="panel-desc">Play native Windows desktop chime concurrently with the edge glow</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Enable Sound Chime</span>
                      <span class="control-sub">Trigger native sound alert when notifications are captured</span>
                    </div>
                    <button
                      class="switch-control {appSettings.sound_enabled ? 'on' : 'off'}"
                      onclick={() => {
                        appSettings.sound_enabled = !appSettings.sound_enabled;
                        saveAppSettings();
                      }}
                      role="switch"
                      aria-checked={appSettings.sound_enabled}
                      aria-label="Toggle sound chime"
                    >
                      <span class="switch-ball"></span>
                    </button>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Test Audio Chime</span>
                      <span class="control-sub">Play a test chime through the default Windows audio endpoint</span>
                    </div>
                    <button
                      class="secondary-btn"
                      onclick={testSoundAlert}
                      disabled={isPlayingSound}
                    >
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="btn-icon {isPlayingSound ? 'spin' : ''}">
                        <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"></polygon>
                        <path d="M15.54 8.46a5 5 0 0 1 0 7.07"></path>
                      </svg>
                      <span>{isPlayingSound ? "Playing..." : "Play Sample Chime"}</span>
                    </button>
                  </div>
                </div>
              </section>

              <!-- History Retention & Limits -->
              <section class="card panel-card">
                <div class="panel-header">
                  <div class="panel-title-wrap">
                    <div class="panel-icon-circle">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                        <polyline points="14 2 14 8 20 8"></polyline>
                        <line x1="16" y1="13" x2="8" y2="13"></line>
                        <line x1="16" y1="17" x2="8" y2="17"></line>
                      </svg>
                    </div>
                    <div>
                      <h3 class="panel-title">History Storage & Retention</h3>
                      <p class="panel-desc">Atomic local persistence with bounded memory and disk usage</p>
                    </div>
                  </div>
                </div>

                <div class="controls-list">
                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">Show Notifications Feed</span>
                      <span class="control-sub">Display captured desktop alerts inside the UI</span>
                    </div>
                    <button
                      class="switch-control {appSettings.show_notifications ? 'on' : 'off'}"
                      onclick={() => {
                        appSettings.show_notifications = !appSettings.show_notifications;
                        saveAppSettings();
                      }}
                      role="switch"
                      aria-checked={appSettings.show_notifications}
                      aria-label="Toggle show notifications feed"
                    >
                      <span class="switch-ball"></span>
                    </button>
                  </div>

                  <div class="control-item">
                    <div class="control-label-group">
                      <span class="control-title">History Item Limit</span>
                      <span class="control-sub">Maximum notifications stored locally ({appSettings.history_limit} items)</span>
                    </div>
                    <div class="slider-box">
                      <input
                        type="range"
                        min="10"
                        max="500"
                        step="10"
                        bind:value={appSettings.history_limit}
                        oninput={() => saveAppSettingsDebounced(150)}
                        onchange={saveAppSettings}
                        class="native-slider"
                      />
                      <span class="slider-val-badge">{appSettings.history_limit} items</span>
                    </div>
                  </div>
                </div>
              </section>
            </div>
          </div>
        {/if}
      </div>
    {/key}
  </main>

  <!-- Status Bar Footer -->
  <footer class="app-status-bar">
    <div class="status-left">
      <span class="status-item">
        <span class="status-mini-dot {status === 'connected' ? 'ok' : 'err'}"></span>
        IPC: {status === 'connected' ? 'Healthy' : 'Disconnected'}
      </span>
      <span class="status-sep">&bull;</span>
      <span class="status-item">Provider: {pipelineStatus?.provider_name ?? 'Windows Notification'}</span>
      <span class="status-sep">&bull;</span>
      <span class="status-item">Captured: {pipelineStatus?.captured_count ?? notifications.length}</span>
    </div>

    <div class="status-right">
      <span class="status-item">Default: Perpetuity</span>
      <span class="status-sep">&bull;</span>
      <span class="status-item highlight">Single Instance Protected</span>
    </div>
  </footer>

  <!-- Clear All Confirmation Modal Dialog -->
  {#if showClearConfirm}
    <div class="modal-backdrop">
      <button
        type="button"
        class="modal-backdrop-dismiss"
        onclick={() => (showClearConfirm = false)}
        aria-label="Close dialog"
      ></button>
      <div
        class="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="clear-dialog-title"
        tabindex="-1"
      >
        <div class="modal-icon-circle danger">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
          </svg>
        </div>

        <h3 id="clear-dialog-title" class="modal-title">Clear Notification History?</h3>
        <p class="modal-body">
          Are you sure you want to permanently delete all {notifications.length} stored notifications? This removes local records from disk and cannot be undone.
        </p>

        <div class="modal-footer-btns">
          <button
            class="secondary-btn"
            onclick={() => (showClearConfirm = false)}
            disabled={isClearing}
          >
            Cancel
          </button>
          <button
            class="danger-btn"
            onclick={confirmClearAll}
            disabled={isClearing}
          >
            {isClearing ? "Deleting..." : "Yes, Delete All"}
          </button>
        </div>
      </div>
    </div>
  {/if}
</div>

<style>
  /* ========================================================================= */
  /* WINDOWS 11 DESIGN SYSTEM & THEME TOKENS                                  */
  /* ========================================================================= */
  :global(body) {
    margin: 0;
    padding: 0;
    background-color: #0b0f19;
    color: #f8fafc;
    font-family: "Segoe UI Variable Display", "Segoe UI", -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif;
    user-select: none;
    overflow: hidden;
    transition: background-color 0.2s ease, color 0.15s ease;
  }

  /* Thin Theme-Aware Desktop Scrollbar */
  ::-webkit-scrollbar {
    width: 6px;
    height: 6px;
  }
  ::-webkit-scrollbar-track {
    background: transparent;
  }
  ::-webkit-scrollbar-thumb {
    background: var(--border);
    border-radius: 3px;
  }
  ::-webkit-scrollbar-thumb:hover {
    background: var(--border-strong);
  }

  /* Root Base Themes */
  :global(body[data-theme="perpetuity"]) { background-color: #0b0f19; color: #f8fafc; }
  :global(body[data-theme="catppuccin"]) { background-color: #181825; color: #cdd6f4; }
  :global(body[data-theme="vintage-paper"]) { background-color: #f4efe6; color: #241e17; }
  :global(body[data-theme="amethyst-haze"]) { background-color: #0e0b16; color: #f5f0ff; }
  :global(body[data-theme="sage-mist"]) { background-color: #0c1310; color: #ecfdf5; }
  :global(body[data-theme="bubblegum"]) { background-color: #140a14; color: #fdf2f8; }
  :global(body[data-theme="amberstate"]) { background-color: #0d1117; color: #f8fafc; }

  /* ------------------------------------------------------------------------- */
  /* THEME 1: PERPETUITY (DEFAULT)                                             */
  /* ------------------------------------------------------------------------- */
  .shell,
  .shell[data-theme="perpetuity"] {
    --bg: #0b0f19;
    --bg-secondary: #070a11;
    --surface: #121826;
    --surface-elevated: #1a2336;
    --surface-hover: #222f47;
    --border: rgba(255, 255, 255, 0.09);
    --border-strong: rgba(99, 102, 241, 0.45);
    --text-primary: #f8fafc;
    --text-secondary: #e2e8f0;
    --text-muted: #94a3b8;
    --accent: #6366f1;
    --accent-hover: #4f46e5;
    --accent-fg: #ffffff;
    --accent-gradient: linear-gradient(135deg, #6366f1 0%, #38bdf8 100%);
    --header-glow: radial-gradient(circle, rgba(99, 102, 241, 0.16) 0%, rgba(56, 189, 248, 0.08) 50%, transparent 70%);
    --glow-surface: rgba(99, 102, 241, 0.2);
    --ring: #6366f1;
    --danger: #ef4444;
    --danger-bg: rgba(239, 68, 68, 0.14);
    --danger-border: rgba(239, 68, 68, 0.35);
    --danger-text: #fca5a5;
    --warning: #f59e0b;
    --warning-bg: rgba(245, 158, 11, 0.14);
    --warning-border: rgba(245, 158, 11, 0.35);
    --warning-text: #fbbf24;
    --success: #10b981;
    --success-bg: rgba(16, 185, 129, 0.14);
    --success-border: rgba(16, 185, 129, 0.35);
    --success-text: #34d399;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 2: CATPPUCCIN                                                       */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="catppuccin"] {
    --bg: #181825;
    --bg-secondary: #11111b;
    --surface: #1e1e2e;
    --surface-elevated: #313244;
    --surface-hover: #3b3c52;
    --border: rgba(147, 153, 178, 0.2);
    --border-strong: rgba(203, 166, 247, 0.45);
    --text-primary: #cdd6f4;
    --text-secondary: #bac2de;
    --text-muted: #a6adc8;
    --accent: #cba6f7;
    --accent-hover: #b4befe;
    --accent-fg: #11111b;
    --accent-gradient: linear-gradient(135deg, #cba6f7 0%, #89b4fa 100%);
    --header-glow: radial-gradient(circle, rgba(203, 166, 247, 0.16) 0%, rgba(137, 180, 250, 0.05) 50%, transparent 70%);
    --glow-surface: rgba(203, 166, 247, 0.2);
    --ring: #cba6f7;
    --danger: #f38ba8;
    --danger-bg: rgba(243, 139, 168, 0.14);
    --danger-border: rgba(243, 139, 168, 0.35);
    --danger-text: #f38ba8;
    --warning: #f9e2af;
    --warning-bg: rgba(249, 226, 175, 0.14);
    --warning-border: rgba(249, 226, 175, 0.35);
    --warning-text: #f9e2af;
    --success: #a6e3a1;
    --success-bg: rgba(166, 227, 161, 0.14);
    --success-border: rgba(166, 227, 161, 0.35);
    --success-text: #a6e3a1;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 3: VINTAGE PAPER                                                    */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="vintage-paper"] {
    --bg: #f4efe6;
    --bg-secondary: #ebe4d8;
    --surface: #faf6f0;
    --surface-elevated: #ede5d8;
    --surface-hover: #e4dbc9;
    --border: rgba(168, 150, 130, 0.38);
    --border-strong: rgba(139, 94, 52, 0.5);
    --text-primary: #241e17;
    --text-secondary: #4d4034;
    --text-muted: #7a6a5b;
    --accent: #8b5e34;
    --accent-hover: #724b26;
    --accent-fg: #ffffff;
    --accent-gradient: linear-gradient(135deg, #8b5e34 0%, #b8860b 100%);
    --header-glow: radial-gradient(circle, rgba(139, 94, 52, 0.12) 0%, rgba(184, 134, 11, 0.05) 50%, transparent 70%);
    --glow-surface: rgba(139, 94, 52, 0.16);
    --ring: #8b5e34;
    --danger: #b91c1c;
    --danger-bg: rgba(185, 28, 28, 0.1);
    --danger-border: rgba(185, 28, 28, 0.3);
    --danger-text: #991b1b;
    --warning: #b8860b;
    --warning-bg: rgba(184, 134, 11, 0.12);
    --warning-border: rgba(184, 134, 11, 0.3);
    --warning-text: #8b5a04;
    --success: #2d6a4f;
    --success-bg: rgba(45, 106, 79, 0.12);
    --success-border: rgba(45, 106, 79, 0.3);
    --success-text: #2d6a4f;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 4: AMETHYST HAZE                                                    */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="amethyst-haze"] {
    --bg: #0e0b16;
    --bg-secondary: #08060c;
    --surface: #191329;
    --surface-elevated: #281c40;
    --surface-hover: #352554;
    --border: rgba(168, 85, 247, 0.22);
    --border-strong: rgba(192, 132, 252, 0.45);
    --text-primary: #f5f0ff;
    --text-secondary: #e9d5ff;
    --text-muted: #c084fc;
    --accent: #a855f7;
    --accent-hover: #9333ea;
    --accent-fg: #ffffff;
    --accent-gradient: linear-gradient(135deg, #a855f7 0%, #ec4899 100%);
    --header-glow: radial-gradient(circle, rgba(168, 85, 247, 0.18) 0%, rgba(236, 72, 153, 0.08) 50%, transparent 70%);
    --glow-surface: rgba(168, 85, 247, 0.22);
    --ring: #a855f7;
    --danger: #f43f5e;
    --danger-bg: rgba(244, 63, 94, 0.14);
    --danger-border: rgba(244, 63, 94, 0.35);
    --danger-text: #fb7185;
    --warning: #fbbf24;
    --warning-bg: rgba(251, 191, 36, 0.14);
    --warning-border: rgba(251, 191, 36, 0.35);
    --warning-text: #fbbf24;
    --success: #34d399;
    --success-bg: rgba(52, 211, 153, 0.14);
    --success-border: rgba(52, 211, 153, 0.35);
    --success-text: #34d399;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 5: SAGE MIST                                                        */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="sage-mist"] {
    --bg: #0c1310;
    --bg-secondary: #070b09;
    --surface: #121e18;
    --surface-elevated: #1a2c24;
    --surface-hover: #233a30;
    --border: rgba(52, 211, 153, 0.22);
    --border-strong: rgba(52, 211, 153, 0.45);
    --text-primary: #ecfdf5;
    --text-secondary: #d1fae5;
    --text-muted: #a7f3d0;
    --accent: #34d399;
    --accent-hover: #10b981;
    --accent-fg: #064e3b;
    --accent-gradient: linear-gradient(135deg, #34d399 0%, #14b8a6 100%);
    --header-glow: radial-gradient(circle, rgba(52, 211, 153, 0.16) 0%, rgba(20, 184, 166, 0.08) 50%, transparent 70%);
    --glow-surface: rgba(52, 211, 153, 0.2);
    --ring: #34d399;
    --danger: #f87171;
    --danger-bg: rgba(248, 113, 113, 0.14);
    --danger-border: rgba(248, 113, 113, 0.35);
    --danger-text: #fca5a5;
    --warning: #f59e0b;
    --warning-bg: rgba(245, 158, 11, 0.14);
    --warning-border: rgba(245, 158, 11, 0.35);
    --warning-text: #fbbf24;
    --success: #10b981;
    --success-bg: rgba(16, 185, 129, 0.14);
    --success-border: rgba(16, 185, 129, 0.35);
    --success-text: #34d399;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 6: BUBBLEGUM                                                        */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="bubblegum"] {
    --bg: #140a14;
    --bg-secondary: #0c050c;
    --surface: #221022;
    --surface-elevated: #321832;
    --surface-hover: #452145;
    --border: rgba(244, 63, 94, 0.22);
    --border-strong: rgba(251, 113, 133, 0.45);
    --text-primary: #fdf2f8;
    --text-secondary: #fce7f3;
    --text-muted: #fda4af;
    --accent: #f43f5e;
    --accent-hover: #e11d48;
    --accent-fg: #ffffff;
    --accent-gradient: linear-gradient(135deg, #f43f5e 0%, #c084fc 100%);
    --header-glow: radial-gradient(circle, rgba(244, 63, 94, 0.18) 0%, rgba(192, 132, 252, 0.08) 50%, transparent 70%);
    --glow-surface: rgba(244, 63, 94, 0.22);
    --ring: #f43f5e;
    --danger: #e11d48;
    --danger-bg: rgba(225, 29, 72, 0.14);
    --danger-border: rgba(225, 29, 72, 0.35);
    --danger-text: #fda4af;
    --warning: #facc15;
    --warning-bg: rgba(250, 204, 21, 0.14);
    --warning-border: rgba(250, 204, 21, 0.35);
    --warning-text: #fde047;
    --success: #4ade80;
    --success-bg: rgba(74, 222, 128, 0.14);
    --success-border: rgba(74, 222, 128, 0.35);
    --success-text: #4ade80;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME 7: AMBERSTATE                                                       */
  /* ------------------------------------------------------------------------- */
  .shell[data-theme="amberstate"] {
    --bg: #0d1117;
    --bg-secondary: #080a0e;
    --surface: #161b22;
    --surface-elevated: #212833;
    --surface-hover: #2b3543;
    --border: rgba(245, 158, 11, 0.22);
    --border-strong: rgba(251, 191, 36, 0.45);
    --text-primary: #f8fafc;
    --text-secondary: #e2e8f0;
    --text-muted: #cbd5e1;
    --accent: #f59e0b;
    --accent-hover: #d97706;
    --accent-fg: #1a1003;
    --accent-gradient: linear-gradient(135deg, #f59e0b 0%, #ea580c 100%);
    --header-glow: radial-gradient(circle, rgba(245, 158, 11, 0.18) 0%, rgba(234, 88, 12, 0.08) 50%, transparent 70%);
    --glow-surface: rgba(245, 158, 11, 0.22);
    --ring: #f59e0b;
    --danger: #ef4444;
    --danger-bg: rgba(239, 68, 68, 0.14);
    --danger-border: rgba(239, 68, 68, 0.35);
    --danger-text: #fca5a5;
    --warning: #f59e0b;
    --warning-bg: rgba(245, 158, 11, 0.14);
    --warning-border: rgba(245, 158, 11, 0.35);
    --warning-text: #fde68a;
    --success: #10b981;
    --success-bg: rgba(16, 185, 129, 0.14);
    --success-border: rgba(16, 185, 129, 0.35);
    --success-text: #34d399;
  }

  /* ------------------------------------------------------------------------- */
  /* SHELL LAYOUT                                                              */
  /* ------------------------------------------------------------------------- */
  .shell {
    position: relative;
    display: flex;
    flex-direction: column;
    height: 100vh;
    box-sizing: border-box;
    overflow: hidden;
    background-color: var(--bg);
    color: var(--text-primary);
  }

  .ambient-glow-mesh {
    position: absolute;
    top: -120px;
    right: -100px;
    width: 520px;
    height: 520px;
    background: var(--header-glow);
    pointer-events: none;
    z-index: 0;
    transition: background 0.25s ease;
  }

  /* ------------------------------------------------------------------------- */
  /* HEADER WITH INTEGRATED TOP TABS & INTERACTIVE LISTENER CONTROL            */
  /* ------------------------------------------------------------------------- */
  .app-header {
    position: relative;
    z-index: 20;
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 20px;
    background: var(--surface);
    border-bottom: 1px solid var(--border);
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
  }

  .header-left {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-shrink: 0;
  }

  .brand-badge {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 30px;
    height: 30px;
    border-radius: 7px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    box-shadow: 0 0 12px var(--glow-surface);
    flex-shrink: 0;
    overflow: hidden;
  }

  .brand-icon-img {
    width: 22px;
    height: 22px;
    border-radius: 4px;
    object-fit: contain;
  }

  .brand-row {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .brand-title {
    margin: 0;
    font-size: 15px;
    font-weight: 700;
    letter-spacing: -0.02em;
    color: var(--text-primary);
  }

  .brand-version-pill {
    font-size: 10px;
    font-weight: 600;
    padding: 1px 5px;
    border-radius: 4px;
    background: var(--surface-elevated);
    color: var(--text-muted);
    font-family: monospace;
  }

  /* Top Tabs (Center) */
  .header-center-tabs {
    display: flex;
    align-items: center;
    gap: 4px;
    background: var(--surface-elevated);
    padding: 3px;
    border-radius: 8px;
    border: 1px solid var(--border);
  }

  .nav-tab {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 6px 14px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 500;
    color: var(--text-muted);
    background: transparent;
    border: 1px solid transparent;
    cursor: pointer;
    transition: background-color 0.15s ease, color 0.15s ease, border-color 0.15s ease;
  }

  .nav-tab:hover {
    color: var(--text-primary);
    background: var(--surface-hover);
  }

  .nav-tab.active {
    background: var(--surface);
    color: var(--accent);
    border-color: var(--border);
    font-weight: 600;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.12);
  }

  .nav-tab:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 1px;
  }

  .nav-icon {
    width: 14px;
    height: 14px;
  }

  .nav-badge-pill {
    background: var(--accent);
    color: var(--accent-fg);
    font-size: 10px;
    font-weight: 700;
    padding: 1px 5px;
    border-radius: 10px;
    line-height: 1.2;
  }

  /* Right: Interactive Listening Status Button (Authoritative Toggle) */
  .header-right {
    display: flex;
    align-items: center;
    flex-shrink: 0;
  }

  .listening-toggle-btn {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 5px 12px;
    border-radius: 20px;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    border: 1px solid transparent;
    background: var(--surface-elevated);
    transition: transform 0.15s ease, background-color 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease;
  }

  .listening-toggle-btn:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
  }

  .listening-toggle-btn:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .listening-toggle-btn.ok {
    background: var(--success-bg);
    border-color: var(--success-border);
    color: var(--success-text);
  }

  .listening-toggle-btn.ok:hover:not(:disabled) {
    background: var(--success-bg);
    border-color: var(--success);
  }

  .listening-toggle-btn.warn,
  .listening-toggle-btn.paused {
    background: var(--warning-bg);
    border-color: var(--warning-border);
    color: var(--warning-text);
  }

  .listening-toggle-btn.warn:hover:not(:disabled),
  .listening-toggle-btn.paused:hover:not(:disabled) {
    border-color: var(--warning);
  }

  .listening-toggle-btn.err {
    background: var(--danger-bg);
    border-color: var(--danger-border);
    color: var(--danger-text);
  }

  .status-dot-pulse {
    width: 7px;
    height: 7px;
    border-radius: 50%;
  }

  .status-dot-pulse.ok {
    background-color: var(--success);
    box-shadow: 0 0 8px var(--success);
    animation: dot-pulse 2s infinite ease-in-out;
  }

  .status-dot-pulse.warn,
  .status-dot-pulse.paused {
    background-color: var(--warning);
    box-shadow: 0 0 6px var(--warning);
  }

  .status-dot-pulse.err {
    background-color: var(--danger);
    box-shadow: 0 0 8px var(--danger);
  }

  @keyframes dot-pulse {
    0%, 100% { transform: scale(1); opacity: 1; }
    50% { transform: scale(1.2); opacity: 0.75; }
  }

  .status-text-label {
    letter-spacing: -0.01em;
  }

  /* ------------------------------------------------------------------------- */
  /* MAIN CONTENT VIEWPORT & 21ST.DEV-INSPIRED DIRECTIONAL HERO TRANSITIONS     */
  /* ------------------------------------------------------------------------- */
  .main-viewport {
    flex: 1;
    overflow-y: auto;
    overflow-x: hidden;
    padding: 20px 28px;
    box-sizing: border-box;
    position: relative;
    z-index: 1;
  }

  .hero-page-container {
    animation-duration: 220ms;
    animation-timing-function: cubic-bezier(0.16, 1, 0.3, 1);
    animation-fill-mode: both;
  }

  .hero-page-container.page-forward {
    animation-name: hero-enter-forward;
  }

  .hero-page-container.page-backward {
    animation-name: hero-enter-backward;
  }

  .hero-page-container.page-none,
  .hero-page-container:not(.page-forward):not(.page-backward) {
    animation-name: hero-enter-none;
  }

  @keyframes hero-enter-forward {
    0% {
      opacity: 0;
      transform: translate3d(20px, 0, 0);
      filter: blur(3px);
    }
    100% {
      opacity: 1;
      transform: translate3d(0, 0, 0);
      filter: blur(0);
    }
  }

  @keyframes hero-enter-backward {
    0% {
      opacity: 0;
      transform: translate3d(-20px, 0, 0);
      filter: blur(3px);
    }
    100% {
      opacity: 1;
      transform: translate3d(0, 0, 0);
      filter: blur(0);
    }
  }

  @keyframes hero-enter-none {
    0% {
      opacity: 0;
      transform: translateY(10px);
      filter: blur(3px);
    }
    100% {
      opacity: 1;
      transform: translateY(0);
      filter: blur(0);
    }
  }

  .view-container {
    max-width: 980px;
    margin: 0 auto;
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .view-hero-header {
    margin-bottom: 2px;
  }

  .hero-eyebrow {
    display: inline-block;
    font-size: 10px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--accent);
    margin-bottom: 3px;
    animation: eyebrow-enter 180ms cubic-bezier(0.16, 1, 0.3, 1) both;
  }

  .view-title {
    margin: 0;
    font-size: 18px;
    font-weight: 700;
    letter-spacing: -0.01em;
    color: var(--text-primary);
    animation: title-enter 200ms cubic-bezier(0.16, 1, 0.3, 1) both;
  }

  .view-subtitle {
    margin: 3px 0 0 0;
    font-size: 12px;
    color: var(--text-muted);
    animation: subtitle-enter 240ms cubic-bezier(0.16, 1, 0.3, 1) 30ms both;
  }

  .view-header-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
  }

  @keyframes eyebrow-enter {
    0% { opacity: 0; transform: translateY(4px); }
    100% { opacity: 1; transform: translateY(0); }
  }

  @keyframes title-enter {
    0% { opacity: 0; transform: translateY(6px); }
    100% { opacity: 1; transform: translateY(0); }
  }

  @keyframes subtitle-enter {
    0% { opacity: 0; transform: translateY(8px); }
    100% { opacity: 1; transform: translateY(0); }
  }

  /* ------------------------------------------------------------------------- */
  /* CARDS & PANELS                                                            */
  /* ------------------------------------------------------------------------- */
  .card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
  }

  .panel-card {
    padding: 14px 18px;
  }

  .panel-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
  }

  .panel-title-wrap {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .panel-icon-circle {
    width: 28px;
    height: 28px;
    border-radius: 7px;
    background: var(--surface-elevated);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--accent);
  }

  .panel-icon-circle svg {
    width: 15px;
    height: 15px;
  }

  .panel-title {
    margin: 0;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-primary);
  }

  .panel-desc {
    margin: 2px 0 0 0;
    font-size: 11px;
    color: var(--text-muted);
  }

  .panel-header-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  /* ------------------------------------------------------------------------- */
  /* METRICS GRID (DASHBOARD)                                                  */
  /* ------------------------------------------------------------------------- */
  .metrics-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px;
    animation: content-stagger-enter 260ms cubic-bezier(0.16, 1, 0.3, 1) 60ms both;
  }

  @keyframes content-stagger-enter {
    0% { opacity: 0; transform: translateY(10px); }
    100% { opacity: 1; transform: translateY(0); }
  }

  .metric-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 12px 14px;
    display: flex;
    flex-direction: column;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
    transition: transform 0.15s ease, border-color 0.15s ease;
  }

  .metric-card:hover {
    transform: translateY(-2px);
    border-color: var(--border-strong);
  }

  .metric-top {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .metric-label {
    font-size: 11px;
    font-weight: 600;
    color: var(--text-secondary);
  }

  .metric-icon-wrap {
    width: 24px;
    height: 24px;
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .metric-icon-wrap svg {
    width: 13px;
    height: 13px;
  }

  .metric-icon-wrap.total { background: var(--surface-elevated); color: var(--accent); }
  .metric-icon-wrap.unread { background: var(--surface-elevated); color: var(--accent); }
  .metric-icon-wrap.critical { background: var(--danger-bg); color: var(--danger-text); }
  .metric-icon-wrap.high { background: var(--warning-bg); color: var(--warning-text); }

  .metric-value {
    font-size: 22px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 6px 0 2px 0;
    letter-spacing: -0.02em;
  }

  .metric-foot {
    font-size: 10px;
    color: var(--text-muted);
  }

  /* ------------------------------------------------------------------------- */
  /* SYSTEM STATUS & RECENT FEED (DASHBOARD)                                   */
  /* ------------------------------------------------------------------------- */
  .dashboard-section-row {
    display: flex;
    flex-direction: column;
    gap: 12px;
    animation: content-stagger-enter 260ms cubic-bezier(0.16, 1, 0.3, 1) 80ms both;
  }

  .system-meta-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 10px;
    margin-top: 6px;
  }

  .meta-box {
    display: flex;
    flex-direction: column;
    gap: 3px;
    padding: 8px 10px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    border-radius: 6px;
  }

  .meta-box-label {
    font-size: 10px;
    color: var(--text-muted);
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .meta-box-val {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-primary);
  }

  .status-chip {
    display: inline-block;
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 10px;
    font-weight: 700;
    text-transform: uppercase;
    width: fit-content;
  }

  .status-chip.ok { background: var(--success-bg); color: var(--success-text); }
  .status-chip.warn { background: var(--warning-bg); color: var(--warning-text); }
  .status-chip.err { background: var(--danger-bg); color: var(--danger-text); }

  .permission-warning-banner {
    margin-top: 10px;
    padding: 8px 12px;
    border-radius: 6px;
    background: var(--warning-bg);
    border: 1px solid var(--warning-border);
    color: var(--warning-text);
    font-size: 11px;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .settings-link-btn {
    background: var(--surface);
    border: 1px solid var(--border);
    color: var(--text-primary);
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 11px;
    cursor: pointer;
  }

  .settings-link-btn:hover {
    border-color: var(--border-strong);
  }

  /* Recent Notifications List */
  .recent-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .recent-item-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    border-radius: 8px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    transition: background-color 0.15s ease, border-color 0.15s ease;
  }

  .recent-item-row:hover {
    background: var(--surface-hover);
    border-color: var(--border-strong);
  }

  .recent-item-row.is-unread {
    border-left: 3px solid var(--accent);
  }

  .recent-avatar {
    width: 26px;
    height: 26px;
    border-radius: 6px;
    background: var(--surface);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 700;
    color: var(--text-primary);
    overflow: hidden;
    flex-shrink: 0;
  }

  .recent-avatar-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .recent-content {
    flex: 1;
    min-width: 0;
  }

  .recent-top-line {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 10px;
  }

  .recent-source {
    font-weight: 600;
    color: var(--text-primary);
  }

  .urgency-tag {
    font-size: 9px;
    padding: 1px 4px;
    border-radius: 3px;
    text-transform: uppercase;
    font-weight: 700;
  }

  .urgency-tag.critical { background: var(--danger-bg); color: var(--danger-text); }
  .urgency-tag.high { background: var(--warning-bg); color: var(--warning-text); }
  .urgency-tag.normal { background: var(--surface); color: var(--text-secondary); }

  .recent-time {
    color: var(--text-muted);
    margin-left: auto;
  }

  .recent-title {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .recent-body {
    font-size: 11px;
    color: var(--text-muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .recent-actions {
    display: flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
  }

  .tiny-action-btn {
    width: 22px;
    height: 22px;
    border-radius: 4px;
    background: var(--surface);
    border: 1px solid var(--border);
    color: var(--text-secondary);
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
  }

  .tiny-action-btn:hover {
    background: var(--surface-hover);
    color: var(--text-primary);
  }

  .tiny-action-btn.danger:hover {
    background: var(--danger-bg);
    color: var(--danger-text);
  }

  .tiny-action-btn svg {
    width: 11px;
    height: 11px;
  }

  /* ------------------------------------------------------------------------- */
  /* NOTIFICATION FEED & CARDS                                                 */
  /* ------------------------------------------------------------------------- */
  .feed-header-controls {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .segmented-control {
    display: flex;
    background: var(--surface);
    padding: 2px;
    border-radius: 6px;
    border: 1px solid var(--border);
  }

  .segment-btn {
    padding: 4px 10px;
    border-radius: 4px;
    font-size: 11px;
    font-weight: 500;
    color: var(--text-muted);
    background: transparent;
    border: none;
    cursor: pointer;
    transition: background-color 0.15s ease, color 0.15s ease;
  }

  .segment-btn.active {
    background: var(--surface-elevated);
    color: var(--accent);
    font-weight: 600;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  }

  .segment-btn:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 1px;
  }

  .notification-feed-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
    animation: content-stagger-enter 260ms cubic-bezier(0.16, 1, 0.3, 1) 60ms both;
  }

  .cards-stack {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .notification-card {
    display: flex;
    gap: 12px;
    padding: 12px 16px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
    transition: background-color 0.15s ease, border-color 0.15s ease, transform 0.15s ease;
  }

  .notification-card:hover {
    background: var(--surface-hover);
    border-color: var(--border-strong);
    transform: translateY(-1px);
  }

  .notification-card.is-unread {
    border-left: 3px solid var(--accent);
    background: var(--surface);
  }

  .notification-card.is-read {
    opacity: 0.88;
  }

  .card-avatar-col {
    position: relative;
    display: flex;
    align-items: flex-start;
  }

  .card-avatar {
    width: 34px;
    height: 34px;
    border-radius: 8px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
    overflow: hidden;
  }

  .avatar-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .card-unread-dot {
    position: absolute;
    top: -2px;
    right: -2px;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background-color: var(--accent);
    box-shadow: 0 0 6px var(--accent);
  }

  .card-body-col {
    flex: 1;
    min-width: 0;
  }

  .card-meta-line {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 3px;
  }

  .card-source-tags {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .card-source-name {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-primary);
  }

  .platform-chip {
    font-size: 9px;
    padding: 1px 4px;
    border-radius: 3px;
    background: var(--surface-elevated);
    color: var(--text-muted);
    text-transform: uppercase;
  }

  .urgency-chip {
    font-size: 9px;
    padding: 1px 5px;
    border-radius: 3px;
    text-transform: uppercase;
    font-weight: 700;
  }

  .urgency-chip.critical { background: var(--danger-bg); color: var(--danger-text); }
  .urgency-chip.high { background: var(--warning-bg); color: var(--warning-text); }
  .urgency-chip.normal { background: var(--surface-elevated); color: var(--accent); }

  .card-timestamp {
    font-size: 10px;
    color: var(--text-muted);
  }

  .card-title {
    margin: 0;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-primary);
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    word-break: break-word;
  }

  .card-text {
    margin: 3px 0 6px 0;
    font-size: 12px;
    color: var(--text-muted);
    line-height: 1.4;
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    word-break: break-word;
  }

  .card-footer-line {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .card-id-code {
    font-size: 10px;
    font-family: monospace;
    color: var(--text-muted);
    opacity: 0.75;
  }

  .card-actions-group {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .item-action-btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 3px 8px;
    border-radius: 4px;
    font-size: 10px;
    font-weight: 500;
    cursor: pointer;
    border: 1px solid transparent;
  }

  .item-action-btn.read {
    background: var(--surface-elevated);
    border-color: var(--border);
    color: var(--accent);
  }

  .item-action-btn.read:hover {
    background: var(--surface-hover);
  }

  .item-action-btn.unread {
    background: var(--surface-elevated);
    border-color: var(--border);
    color: var(--text-muted);
  }

  .item-action-btn.dismiss {
    background: var(--danger-bg);
    border-color: var(--danger-border);
    color: var(--danger-text);
  }

  .item-action-btn.dismiss:hover {
    opacity: 0.85;
  }

  /* ------------------------------------------------------------------------- */
  /* EMPTY STATES                                                              */
  /* ------------------------------------------------------------------------- */
  .empty-state-panel {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 32px 16px;
    text-align: center;
    color: var(--text-muted);
  }

  .empty-icon-ring {
    width: 44px;
    height: 44px;
    border-radius: 50%;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 10px;
    color: var(--text-muted);
  }

  .empty-icon-ring svg {
    width: 20px;
    height: 20px;
  }

  .empty-heading {
    margin: 0;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-primary);
  }

  .empty-paragraph {
    margin: 4px 0 12px 0;
    font-size: 11px;
    max-width: 320px;
    color: var(--text-muted);
  }

  /* ------------------------------------------------------------------------- */
  /* CONTROLS & SETTINGS COMPONENTS                                            */
  /* ------------------------------------------------------------------------- */
  .settings-sections-stack {
    display: flex;
    flex-direction: column;
    gap: 12px;
    animation: content-stagger-enter 260ms cubic-bezier(0.16, 1, 0.3, 1) 60ms both;
  }

  .controls-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .control-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 14px;
    padding: 6px 0;
  }

  .control-label-group {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .control-title {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-primary);
  }

  .control-sub {
    font-size: 11px;
    color: var(--text-muted);
  }

  /* Slider */
  .slider-box {
    display: flex;
    align-items: center;
    gap: 10px;
    width: 220px;
  }

  .native-slider {
    flex: 1;
    accent-color: var(--accent);
    cursor: pointer;
  }

  .slider-val-badge {
    font-size: 11px;
    font-weight: 600;
    color: var(--text-secondary);
    min-width: 48px;
    text-align: right;
  }

  /* Switch */
  .switch-control {
    position: relative;
    width: 40px;
    height: 22px;
    border-radius: 11px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    cursor: pointer;
    padding: 2px;
    flex-shrink: 0;
    transition: background-color 0.2s ease, border-color 0.2s ease;
  }

  .switch-control.on {
    background: var(--accent);
    border-color: var(--ring);
    box-shadow: 0 0 10px var(--glow-surface);
  }

  .switch-control:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .switch-ball {
    display: block;
    width: 16px;
    height: 16px;
    border-radius: 50%;
    background: #ffffff;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.35);
    transition: transform 0.2s cubic-bezier(0.16, 1, 0.3, 1);
  }

  .switch-control.on .switch-ball {
    transform: translateX(18px);
  }

  /* Native Select */
  .native-select {
    padding: 6px 12px;
    border-radius: 6px;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    color: var(--text-primary);
    font-size: 12px;
    cursor: pointer;
    outline: none;
  }

  .native-select:focus-visible {
    border-color: var(--ring);
    outline: 2px solid var(--ring);
    outline-offset: 1px;
  }

  /* Color Palette Chips */
  .color-palette-wrap {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }

  .color-dot {
    width: 20px;
    height: 20px;
    border-radius: 50%;
    border: 2px solid transparent;
    cursor: pointer;
    transition: transform 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease;
  }

  .color-dot.selected {
    border-color: var(--text-primary);
    transform: scale(1.18);
    box-shadow: 0 0 8px var(--glow-surface);
  }

  .color-dot:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .color-divider {
    width: 1px;
    height: 18px;
    background: var(--border);
    margin: 0 2px;
  }

  .custom-color-control {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    background: var(--surface-elevated);
    padding: 3px 8px 3px 6px;
    border-radius: 6px;
    border: 1px solid var(--border);
    transition: border-color 0.15s ease;
  }

  .custom-color-control:hover {
    border-color: var(--accent);
  }

  .custom-color-picker-label {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    position: relative;
    color: var(--accent);
  }

  .palette-icon {
    width: 16px;
    height: 16px;
    transition: transform 0.15s ease;
  }

  .custom-color-picker-label:hover .palette-icon {
    transform: rotate(15deg);
  }

  .custom-color-input-hidden {
    position: absolute;
    opacity: 0;
    width: 100%;
    height: 100%;
    top: 0;
    left: 0;
    cursor: pointer;
  }

  .custom-color-hex-tag {
    font-size: 11px;
    font-family: monospace;
    font-weight: 600;
    color: var(--text-muted);
    letter-spacing: 0.05em;
  }

  /* ------------------------------------------------------------------------- */
  /* THEME PICKER GRID                                                         */
  /* ------------------------------------------------------------------------- */
  .themes-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
    gap: 10px;
    margin-top: 4px;
  }

  .theme-picker-card {
    position: relative;
    display: flex;
    flex-direction: column;
    padding: 12px;
    border-radius: 10px;
    background: var(--surface-elevated);
    border: 2px solid var(--border);
    cursor: pointer;
    text-align: left;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
    transition: transform 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease;
  }

  .theme-picker-card:hover {
    border-color: var(--border-strong);
    transform: translateY(-2px);
    box-shadow: 0 6px 16px rgba(0, 0, 0, 0.15);
  }

  .theme-picker-card.active {
    border-color: var(--accent);
    box-shadow: 0 0 14px var(--glow-surface), 0 2px 8px rgba(0, 0, 0, 0.15);
  }

  .theme-picker-card:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .theme-card-top {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 4px;
  }

  .theme-card-title {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
  }

  .theme-selected-pill {
    display: inline-flex;
    align-items: center;
    gap: 3px;
    padding: 2px 6px;
    border-radius: 10px;
    background: var(--accent);
    color: var(--accent-fg);
    font-size: 9px;
    font-weight: 700;
    text-transform: uppercase;
  }

  .theme-card-desc {
    font-size: 10px;
    color: var(--text-muted);
    margin: 0 0 8px 0;
    line-height: 1.35;
    min-height: 28px;
  }

  .theme-swatches-strip {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 8px;
  }

  .theme-swatch-circle {
    width: 14px;
    height: 14px;
    border-radius: 50%;
    border: 1px solid rgba(255, 255, 255, 0.2);
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  }

  .theme-mini-frame {
    border-radius: 6px;
    padding: 7px 9px;
    display: flex;
    flex-direction: column;
    gap: 4px;
    border-width: 1px;
    border-style: solid;
  }

  .mini-frame-header {
    display: flex;
    align-items: center;
    gap: 5px;
  }

  .mini-frame-accent-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
  }

  .mini-frame-bar {
    height: 5px;
    border-radius: 3px;
  }

  .mini-frame-bar.secondary {
    height: 3px;
    opacity: 0.5;
  }

  /* ------------------------------------------------------------------------- */
  /* BUTTONS & ALERTS                                                          */
  /* ------------------------------------------------------------------------- */
  .primary-btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 14px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 600;
    background: var(--accent-gradient);
    border: 1px solid var(--border);
    color: var(--accent-fg);
    cursor: pointer;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
    transition: opacity 0.15s ease, box-shadow 0.15s ease;
  }

  .primary-btn:hover:not(:disabled) {
    opacity: 0.92;
    box-shadow: 0 0 12px var(--glow-surface);
  }

  .primary-btn:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .secondary-btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 500;
    background: var(--surface-elevated);
    border: 1px solid var(--border);
    color: var(--text-primary);
    cursor: pointer;
    transition: background-color 0.15s ease, border-color 0.15s ease;
  }

  .secondary-btn:hover:not(:disabled) {
    background: var(--surface-hover);
    border-color: var(--border-strong);
  }

  .secondary-btn:focus-visible {
    outline: 2px solid var(--ring);
    outline-offset: 2px;
  }

  .danger-btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 600;
    background: var(--danger-bg);
    border: 1px solid var(--danger-border);
    color: var(--danger-text);
    cursor: pointer;
    transition: opacity 0.15s ease;
  }

  .danger-btn:hover:not(:disabled) {
    opacity: 0.85;
  }

  .danger-btn:focus-visible {
    outline: 2px solid var(--danger);
    outline-offset: 2px;
  }

  .text-link-btn {
    background: none;
    border: none;
    color: var(--accent);
    font-size: 11px;
    font-weight: 600;
    cursor: pointer;
    padding: 0;
  }

  .text-link-btn:hover {
    text-decoration: underline;
  }

  .btn-icon {
    width: 14px;
    height: 14px;
  }

  .tiny-icon {
    width: 10px;
    height: 10px;
  }

  .alert-banner {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 12px;
    border-radius: 6px;
    font-size: 12px;
  }

  .alert-banner.danger {
    background: var(--danger-bg);
    border: 1px solid var(--danger-border);
    color: var(--danger-text);
  }

  .banner-close {
    background: none;
    border: none;
    color: inherit;
    font-size: 13px;
    cursor: pointer;
  }

  /* ------------------------------------------------------------------------- */
  /* STATUS BAR FOOTER                                                         */
  /* ------------------------------------------------------------------------- */
  .app-status-bar {
    position: relative;
    z-index: 10;
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 18px;
    background: var(--surface);
    border-top: 1px solid var(--border);
    font-size: 11px;
    color: var(--text-muted);
  }

  .status-left,
  .status-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .status-item {
    display: inline-flex;
    align-items: center;
    gap: 5px;
  }

  .status-item.highlight {
    color: var(--text-secondary);
  }

  .status-mini-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
  }

  .status-mini-dot.ok { background-color: var(--success); }
  .status-mini-dot.err { background-color: var(--danger); }

  .status-sep {
    opacity: 0.4;
  }

  /* ------------------------------------------------------------------------- */
  /* CONFIRMATION MODAL                                                        */
  /* ------------------------------------------------------------------------- */
  .modal-backdrop {
    position: fixed;
    inset: 0;
    z-index: 999;
    background: rgba(0, 0, 0, 0.6);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 16px;
  }

  .modal-backdrop-dismiss {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    background: transparent;
    border: none;
    cursor: pointer;
    padding: 0;
    margin: 0;
  }

  .modal-card {
    position: relative;
    z-index: 1;
    width: 100%;
    max-width: 380px;
    background: var(--surface);
    border: 1px solid var(--border-strong);
    border-radius: 12px;
    padding: 20px;
    box-shadow: 0 16px 40px rgba(0, 0, 0, 0.35);
    display: flex;
    flex-direction: column;
    align-items: center;
    text-align: center;
  }

  .modal-icon-circle {
    width: 44px;
    height: 44px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 12px;
  }

  .modal-icon-circle.danger {
    background: var(--danger-bg);
    color: var(--danger-text);
  }

  .modal-icon-circle svg {
    width: 22px;
    height: 22px;
  }

  .modal-title {
    margin: 0;
    font-size: 15px;
    font-weight: 700;
    color: var(--text-primary);
  }

  .modal-body {
    margin: 6px 0 18px 0;
    font-size: 12px;
    color: var(--text-muted);
    line-height: 1.4;
  }

  .modal-footer-btns {
    display: flex;
    gap: 8px;
    width: 100%;
    justify-content: flex-end;
  }

  .modal-footer-btns button {
    flex: 1;
    justify-content: center;
  }

  /* ------------------------------------------------------------------------- */
  /* ANIMATIONS & ACCESSIBILITY                                                */
  /* ------------------------------------------------------------------------- */
  .spin {
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
  }

  .pulse {
    animation: pulse 1.5s ease-in-out infinite alternate;
  }

  @keyframes pulse {
    0% { transform: scale(1); opacity: 0.8; }
    100% { transform: scale(1.1); opacity: 1; }
  }

  /* Responsive Resizing */
  @media (max-width: 720px) {
    .app-header {
      padding: 8px 12px;
    }
    .header-center-tabs {
      gap: 2px;
    }
    .nav-tab {
      padding: 5px 8px;
      font-size: 11px;
      gap: 5px;
    }
    .brand-version-pill {
      display: none;
    }
    .metrics-grid,
    .system-meta-grid {
      grid-template-columns: repeat(2, 1fr);
    }
  }

  @media (max-width: 540px) {
    .nav-tab span {
      display: none;
    }
    .nav-tab {
      padding: 6px;
    }
    .listening-toggle-btn {
      padding: 4px 8px;
    }
  }

  /* Reduced Motion Accessibility */
  @media (prefers-reduced-motion: reduce) {
    *, ::before, ::after {
      animation-duration: 0.01ms !important;
      animation-iteration-count: 1 !important;
      transition-duration: 0.01ms !important;
      scroll-behavior: auto !important;
    }
    .hero-page-container,
    .hero-page-container.page-forward,
    .hero-page-container.page-backward,
    .hero-page-container.page-none,
    .hero-eyebrow,
    .view-title,
    .view-subtitle,
    .metrics-grid,
    .dashboard-section-row,
    .notification-feed-list,
    .settings-sections-stack {
      animation: none !important;
      transform: none !important;
      filter: none !important;
      opacity: 1 !important;
    }
  }
</style>
