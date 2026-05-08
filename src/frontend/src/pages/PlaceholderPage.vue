<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const title = computed(() => String(route.meta.title ?? 'Chức năng'))
</script>

<template>
  <section class="panel">
    <h2>{{ title }}</h2>
    <p>Màn hình này đã được đặt tuyến để triển khai ở milestone tiếp theo.</p>
  </section>
</template>
