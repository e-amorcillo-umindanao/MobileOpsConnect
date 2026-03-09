/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './Areas/Identity/Pages/**/*.cshtml',
    './wwwroot/js/**/*.js',
    './node_modules/preline/dist/*.js'
  ],
  darkMode: 'class',
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
        },
        // Tokyo Night override for existing Preline/Tailwind dark classes
        neutral: {
          50: '#fafafa',
          100: '#f5f5f5',
          200: '#e5e5e5',
          300: '#d4d4d4',
          400: '#a3a3a3',
          500: '#737373',
          600: '#565f89', // Muted Tokyo text
          700: '#414868', // Tokyo Night borders
          800: '#24283b', // Tokyo Night surface/cards
          900: '#1a1b26', // Tokyo Night deep background
          950: '#16161e', // Tokyo Night darkest
        }
      },      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      }
    },
  },
  plugins: [
    require('preline/plugin')
  ],
}
