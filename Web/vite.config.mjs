import { defineConfig } from 'vite';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  preview: { https: true, host: true },
  plugins: [
    VitePWA({
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.js',
      injectRegister: 'auto',
      injectManifest: {
        injectionPoint: 'self.__WB_MANIFEST',
        globPatterns: ['**/*.{js,css,html,png,svg,ico,json}']
      },
      manifestCrossOrigin: 'use-credentials',
      manifestFilename: 'manifest.json',
      includeAssets: ['**/*'],  // Кэширует всё из public/, включая иконки
      registerType: 'autoUpdate',
      manifest: {
        name: 'Расписание',
        short_name: 'Расписание',
        description: 'Расписание ФГАОУ ВО МГТУ Stankin',
        theme_color: '#ffffff',
        background_color: '#ffffff',
        display: 'standalone',
        lang: 'ru',
        start_url: '/',
        icons: [
          { src: "/icons/icon-128.png", sizes: "128x128", type: "image/png" },
          { src: "/icons/icon-256.png", sizes: "256x256", type: "image/png" },
          { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png" },
          { src: "/icons/icon-1024.png", sizes: "1024x1024", type: "image/png" }
        ]
      }
    })
  ]
});

