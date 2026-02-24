/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './Areas/Identity/Pages/**/*.cshtml',
    './wwwroot/js/**/*.js',
    './node_modules/preline/dist/*.js'
  ],
  theme: {
    extend: {
      colors: {
        moc: {
          primary: '#18181b', // zinc-900
          'primary-hover': '#27272a', // zinc-800
          secondary: '#f4f4f5', // zinc-100
          'secondary-hover': '#e4e4e7', // zinc-200
          dark: '#09090b', // zinc-950
          'dark-light': '#18181b',
          surface: '#ffffff',
          bg: '#fafafa', // zinc-50
          border: '#e4e4e7', // zinc-200
          text: '#09090b', // zinc-950
          'text-muted': '#71717a', // zinc-500
        }
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      }
    },
  },
  plugins: [
    require('preline/plugin')
  ],
}
