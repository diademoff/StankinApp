const colors = require('tailwindcss/colors');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './index.html',
    './about.html',
    './src/**/*.{ts,js,tsx,jsx}'
  ],
  theme: {
    extend: {
      colors: {
        white: 'var(--surface)',
        gray: { ...colors.gray, 50: 'var(--g50)', 100: 'var(--g100)', 200: 'var(--g200)', 400: 'var(--g400)', 500: 'var(--g500)', 600: 'var(--g600)', 700: 'var(--g700)', 800: 'var(--g800)' },
        slate: { ...colors.slate, 50: 'var(--s50)', 200: 'var(--s200)', 300: 'var(--s300)', 700: 'var(--s700)' },
        blue: { ...colors.blue, 50: 'var(--b50)', 100: 'var(--b100)', 200: 'var(--b200)', 300: 'var(--b300)', 400: 'var(--b400)', 500: 'var(--b500)', 600: 'var(--b600)', 700: 'var(--b700)' },
        amber: { ...colors.amber, 100: 'var(--a100)', 700: 'var(--a700)' },
        green: { ...colors.green, 50: 'var(--gr50)', 200: 'var(--gr200)', 500: 'var(--gr500)', 700: 'var(--gr700)' },
        purple: { ...colors.purple, 50: 'var(--p50)', 200: 'var(--p200)', 500: 'var(--p500)', 700: 'var(--p700)' },
        red: { ...colors.red, 500: 'var(--r500)' },
      },
    },
  },
  plugins: [],
};
