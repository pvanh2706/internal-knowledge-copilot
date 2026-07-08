// Cấu hình ESLint flat cho frontend Vue 3 + TypeScript
import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  {
    // Bỏ qua thư mục build và dependencies
    ignores: ['dist/**', 'node_modules/**'],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      parserOptions: {
        // Dùng parser của typescript-eslint cho phần <script lang="ts"> trong file .vue
        parser: tseslint.parser,
      },
    },
    rules: {
      // TypeScript đã kiểm tra định danh không tồn tại (File, Event, HTMLInputElement...);
      // bật no-undef với code TypeScript gây báo lỗi sai — tắt theo khuyến nghị của typescript-eslint
      'no-undef': 'off',
    },
  },
  {
    rules: {
      // Các rule thuần định dạng dưới đây quá ồn với phong cách template gọn một dòng
      // đang dùng nhất quán trong codebase (hơn 900 cảnh báo); tắt để lint tập trung vào lỗi thực sự
      'vue/max-attributes-per-line': 'off',
      'vue/singleline-html-element-content-newline': 'off',
      'vue/html-self-closing': 'off',
    },
  },
)
