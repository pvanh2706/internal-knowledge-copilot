import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './assets/main.css'
import 'vue-sonner/style.css'

const app = createApp(App).use(createPinia()).use(router)

// Chờ router resolve route đầu tiên để App.vue chọn đúng layout ngay lần render đầu
void router.isReady().then(() => {
  app.mount('#app')
})
