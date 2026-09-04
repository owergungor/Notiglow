<script lang="ts">
  import { onMount } from "svelte";
  import { listen } from "@tauri-apps/api/event";

  interface GlowPayload {
    color: string;
    duration_ms: number;
    intensity: number;
    thickness: number;
    corner_radius: number;
    animation_style: "pulse" | "breathing" | "solid";
  }

  let isGlowing = $state(false);
  let payload = $state<GlowPayload>({
    color: "#6366f1",
    duration_ms: 2500,
    intensity: 0.8,
    thickness: 8,
    corner_radius: 24,
    animation_style: "pulse",
  });
  let timer: ReturnType<typeof setTimeout> | null = null;

  onMount(() => {
    const unlistenPromise = listen<GlowPayload>("trigger-glow", (event) => {
      payload = event.payload;
      isGlowing = true;

      if (timer) clearTimeout(timer);
      timer = setTimeout(() => {
        isGlowing = false;
      }, payload.duration_ms);
    });

    return () => {
      if (timer) clearTimeout(timer);
      unlistenPromise.then((unlisten) => unlisten());
    };
  });
</script>

<div
  class="glow-viewport {isGlowing ? 'active' : ''} {payload.animation_style}"
  style:--glow-color={payload.color}
  style:--glow-thickness="{payload.thickness}px"
  style:--glow-radius="{payload.corner_radius}px"
  style:--glow-intensity={payload.intensity}
  style:--glow-duration="{payload.duration_ms}ms"
>
  <div class="glow-edge-inner"></div>
</div>

<style>
  :global(html, body) {
    margin: 0 !important;
    padding: 0 !important;
    background: transparent !important;
    overflow: hidden !important;
    user-select: none !important;
    pointer-events: none !important;
    width: 100vw;
    height: 100vh;
  }

  .glow-viewport {
    position: fixed;
    inset: 0;
    pointer-events: none;
    opacity: 0;
    transition: opacity 0.25s ease-out;
    box-sizing: border-box;
    will-change: opacity, filter;
  }

  .glow-viewport.active {
    opacity: 1;
  }

  .glow-edge-inner {
    position: absolute;
    inset: 0;
    border-radius: var(--glow-radius);
    pointer-events: none;
    box-sizing: border-box;
    border: calc(var(--glow-thickness) * 0.5) solid var(--glow-color);
    box-shadow:
      inset 0 0 calc(var(--glow-thickness) * 1.5) var(--glow-color),
      inset 0 0 calc(var(--glow-thickness) * 3) var(--glow-color),
      0 0 calc(var(--glow-thickness) * 2) var(--glow-color);
    opacity: var(--glow-intensity);
    will-change: opacity, transform, filter;
  }

  /* Pulse Animation: Periodic rhythmic pulses */
  .glow-viewport.pulse.active .glow-edge-inner {
    animation: pulse-glow 0.85s cubic-bezier(0.4, 0, 0.2, 1) infinite;
  }

  @keyframes pulse-glow {
    0% {
      opacity: calc(var(--glow-intensity) * 0.25);
      transform: scale(0.996);
      filter: brightness(0.85) blur(0.5px);
    }
    30% {
      opacity: var(--glow-intensity);
      transform: scale(1);
      filter: brightness(1.35) blur(1.5px);
    }
    60% {
      opacity: calc(var(--glow-intensity) * 0.35);
      transform: scale(0.998);
      filter: brightness(0.9) blur(0.5px);
    }
    100% {
      opacity: calc(var(--glow-intensity) * 0.25);
      transform: scale(0.996);
      filter: brightness(0.85) blur(0.5px);
    }
  }

  /* Breathing Animation: Slower, organic, continuous harmonic wave */
  .glow-viewport.breathing.active .glow-edge-inner {
    animation: breathe-glow 2.8s ease-in-out infinite;
  }

  @keyframes breathe-glow {
    0% {
      opacity: calc(var(--glow-intensity) * 0.3);
      transform: scale(0.998);
      filter: brightness(0.85) blur(0.5px);
    }
    50% {
      opacity: var(--glow-intensity);
      transform: scale(1.001);
      filter: brightness(1.15) blur(2px);
    }
    100% {
      opacity: calc(var(--glow-intensity) * 0.3);
      transform: scale(0.998);
      filter: brightness(0.85) blur(0.5px);
    }
  }

  /* Solid Animation: Completely stationary, crisp, stable outline without any periodic opacity animation */
  .glow-viewport.solid.active .glow-edge-inner {
    animation: none !important;
    opacity: var(--glow-intensity) !important;
    transform: none !important;
    filter: brightness(1.05) !important;
  }

  /* Accessibility: Reduced Motion */
  @media (prefers-reduced-motion: reduce) {
    .glow-viewport {
      transition: none !important;
    }
    .glow-viewport.pulse.active .glow-edge-inner,
    .glow-viewport.breathing.active .glow-edge-inner {
      animation: none !important;
    }
    .glow-edge-inner {
      opacity: calc(var(--glow-intensity) * 0.5) !important;
      filter: none !important;
    }
  }
</style>
