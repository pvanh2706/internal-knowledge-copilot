import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { AuthUser } from '../api/auth'
import { login as loginRequest } from '../api/auth'

const tokenStorageKey = 'ikc.accessToken'
const userStorageKey = 'ikc.user'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(localStorage.getItem(tokenStorageKey))
  const rawUser = localStorage.getItem(userStorageKey)
  const user = ref<AuthUser | null>(rawUser ? (JSON.parse(rawUser) as AuthUser) : null)

  const isAuthenticated = computed(() => Boolean(accessToken.value && user.value))
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isReviewer = computed(() => user.value?.role === 'Reviewer')

  async function login(email: string, password: string) {
    const response = await loginRequest(email, password)
    accessToken.value = response.accessToken
    user.value = response.user
    localStorage.setItem(tokenStorageKey, response.accessToken)
    localStorage.setItem(userStorageKey, JSON.stringify(response.user))
    return response
  }

  function logout() {
    accessToken.value = null
    user.value = null
    localStorage.removeItem(tokenStorageKey)
    localStorage.removeItem(userStorageKey)
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    isAdmin,
    isReviewer,
    login,
    logout,
  }
})
