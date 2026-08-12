module.exports = {
  content: ['./index.html', './apps.html', './about.html', './download.html', './releases.html', './privacy.html', './app/*.html', './assets/app.js'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      colors: {
        indigo: {
          950: '#1e1b4b',
        },
      },
    },
  },
  plugins: [],
}
