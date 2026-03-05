/** @type {import('tailwindcss').Config} */


// Helper: wrap a CSS variable name as a Tailwind color value.
// Tailwind calls the function with an `{ opacityValue }` argument when you
// use opacity modifiers (e.g. bg-accent/20), so we return an rgba() form then.
function cssVar(name) {
  return ({ opacityValue }) =>
      opacityValue !== undefined
          ? `rgba(var(${name}-rgb), ${opacityValue})`
          : `var(${name})`
}

export default {
  content: ['./src/**/*.{svelte,js,ts}', './index.html'],
  theme: {
    extend: {
      fontFamily: {
        mono:    ['"JetBrains Mono"', '"Fira Code"', 'monospace'],
        display: ['"Space Grotesk"', 'sans-serif'],
        body:    ['"DM Sans"', 'sans-serif'],
      },
      colors: {
        bg:      'var(--color-bg)',
        surface: 'var(--color-surface)',
        border:  'var(--color-border)',
        accent:  'var(--color-accent)',
        green:   'var(--color-green)',
        yellow:  'var(--color-yellow)',
        red:     'var(--color-red)',
        muted:   'var(--color-muted)',
        text:    'var(--color-text)',
      },
      animation: {
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'slide-in':   'slideIn 0.3s ease-out',
      },
      keyframes: {
        slideIn: {
          '0%':   { transform: 'translateX(-8px)', opacity: '0' },
          '100%': { transform: 'translateX(0)',     opacity: '1' },
        },
      },
    },
  },
  plugins: []
}
