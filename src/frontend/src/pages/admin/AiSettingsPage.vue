<script setup lang="ts">
import { onMounted } from 'vue'
import { FlaskConicalIcon, RefreshCwIcon, SaveIcon } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import ErrorState from '@/components/app/ErrorState.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatCard from '@/components/app/StatCard.vue'
import StatusBadge from '@/components/app/StatusBadge.vue'
import { useAiSettingsForm } from '@/composables/useAiSettingsForm'

const {
  form,
  isLoading,
  isSaving,
  isTesting,
  useSharedKeyForEmbedding,
  errorMessage,
  successMessage,
  testResult,
  hasSavedApiKey,
  hasSavedEmbeddingApiKey,
  updatedText,
  isMockProvider,
  isOpenAiProvider,
  isAnthropicProvider,
  isExternalProvider,
  hasEffectiveEmbeddingKey,
  canSave,
  providerStatus,
  onProviderChange,
  loadSettings,
  saveSettings,
  submitTest,
} = useAiSettingsForm()

onMounted(loadSettings)
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <PageHeader
      title="Cấu hình AI"
      description="Chọn nhà cung cấp và nhập API key. Các thông số model, endpoint và embedding đã có preset mặc định."
    >
      <template #actions>
        <Button variant="outline" :disabled="isLoading" @click="loadSettings">
          <RefreshCwIcon class="size-4" />
          Làm mới
        </Button>
      </template>
    </PageHeader>

    <ErrorState v-if="errorMessage" :message="errorMessage" class="mb-4" @retry="loadSettings" />
    <p
      v-if="successMessage"
      class="mb-4 rounded-md border border-success/30 bg-success/10 p-3 text-sm text-success"
      role="status"
    >
      {{ successMessage }}
    </p>

    <div class="mb-6 grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Nhà cung cấp" :value="providerStatus" />
      <StatCard label="API key" :value="hasSavedApiKey ? 'Đã lưu' : 'Chưa có'" />
      <StatCard label="Embedding" :value="hasEffectiveEmbeddingKey || isMockProvider ? 'Sẵn sàng' : 'Chưa có key'" />
      <StatCard label="Cập nhật" value="" :hint="updatedText" />
    </div>

    <form @submit.prevent="saveSettings(false)">
      <Tabs default-value="basic">
        <TabsList>
          <TabsTrigger value="basic">Cơ bản</TabsTrigger>
          <TabsTrigger value="advanced">Nâng cao</TabsTrigger>
        </TabsList>

        <TabsContent value="basic">
          <Card>
            <CardContent class="grid max-w-xl gap-4 pt-6">
              <div class="grid gap-1.5">
                <Label for="ai-provider">Nhà cung cấp</Label>
                <Select v-model="form.name" @update:model-value="onProviderChange">
                  <SelectTrigger id="ai-provider" class="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="mock">Mock local</SelectItem>
                    <SelectItem value="openai">OpenAI</SelectItem>
                    <SelectItem value="anthropic">Anthropic / Claude</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div v-if="isExternalProvider" class="grid gap-1.5">
                <Label for="ai-api-key">{{ isAnthropicProvider ? 'Anthropic API key' : 'OpenAI API key' }}</Label>
                <Input
                  id="ai-api-key"
                  v-model="form.apiKey"
                  type="text"
                  autocomplete="off"
                  :required="!hasSavedApiKey"
                  :placeholder="hasSavedApiKey ? 'API key đang lưu' : isAnthropicProvider ? 'Dán API key của Anthropic' : 'Dán API key của OpenAI'"
                />
              </div>

              <label v-if="isExternalProvider && hasSavedApiKey" class="flex cursor-pointer items-center gap-2.5 text-sm">
                <Checkbox v-model="form.clearApiKey" />
                Xóa API key đang lưu
              </label>

              <p v-if="isMockProvider" class="rounded-md border-l-2 border-info bg-info/10 p-3 text-sm text-muted-foreground">
                Mock local không gọi LLM bên ngoài. Chế độ này chỉ phù hợp để demo luồng, câu trả lời sẽ là bản ghép từ
                các đoạn tìm kiếm.
              </p>
              <p v-else class="rounded-md border-l-2 border-info bg-info/10 p-3 text-sm text-muted-foreground">
                Preset {{ isAnthropicProvider ? 'Anthropic' : 'OpenAI' }} dùng endpoint {{ form.chatEndpointMode }},
                model {{ form.chatModel }}.
                {{ isAnthropicProvider ? 'Embedding đang dùng mock để không cần thêm key khác.' : 'Embedding sẽ dùng chung API key ở trên.' }}
              </p>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="advanced">
          <Card>
            <CardContent class="grid gap-5 pt-6">
              <div class="grid gap-4 sm:grid-cols-2">
                <div class="grid gap-1.5">
                  <Label for="ai-base-url">Base URL</Label>
                  <Input id="ai-base-url" v-model.trim="form.baseUrl" type="url" />
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-endpoint-mode">Chat endpoint</Label>
                  <Select v-model="form.chatEndpointMode" :disabled="isMockProvider">
                    <SelectTrigger id="ai-endpoint-mode" class="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="responses">Responses API</SelectItem>
                      <SelectItem value="chat-completions">Chat Completions</SelectItem>
                      <SelectItem value="messages">Anthropic Messages</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-chat-model">Chat model</Label>
                  <Input id="ai-chat-model" v-model.trim="form.chatModel" type="text" :disabled="isMockProvider" />
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-fast-model">Fast model</Label>
                  <Input id="ai-fast-model" v-model.trim="form.fastModel" type="text" :disabled="isMockProvider" />
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-reasoning">Reasoning effort</Label>
                  <Select v-model="form.reasoningEffort" :disabled="isMockProvider">
                    <SelectTrigger id="ai-reasoning" class="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="low">low</SelectItem>
                      <SelectItem value="medium">medium</SelectItem>
                      <SelectItem value="high">high</SelectItem>
                      <SelectItem value="xhigh">xhigh</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-timeout">Timeout (giây)</Label>
                  <Input id="ai-timeout" v-model.number="form.timeoutSeconds" type="number" min="1" max="600" />
                </div>
              </div>

              <label v-if="isOpenAiProvider" class="flex cursor-pointer items-center gap-2.5 text-sm">
                <Checkbox v-model="useSharedKeyForEmbedding" />
                Dùng chung OpenAI API key cho embedding
              </label>

              <div class="grid gap-4 sm:grid-cols-2">
                <div class="grid gap-1.5">
                  <Label for="ai-embedding-provider">Embedding provider</Label>
                  <Select v-model="form.embeddingProviderName" :disabled="isMockProvider || isAnthropicProvider">
                    <SelectTrigger id="ai-embedding-provider" class="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="openai">OpenAI</SelectItem>
                      <SelectItem value="mock">Mock</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-embedding-model">Embedding model</Label>
                  <Input
                    id="ai-embedding-model"
                    v-model.trim="form.embeddingModel"
                    type="text"
                    :disabled="isMockProvider || form.embeddingProviderName === 'mock'"
                  />
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-embedding-base-url">Embedding Base URL</Label>
                  <Input
                    id="ai-embedding-base-url"
                    v-model.trim="form.embeddingBaseUrl"
                    type="url"
                    :disabled="isMockProvider || form.embeddingProviderName === 'mock'"
                  />
                </div>
                <div class="grid gap-1.5">
                  <Label for="ai-embedding-dimension">Embedding dimension</Label>
                  <Input
                    id="ai-embedding-dimension"
                    v-model.number="form.embeddingDimension"
                    type="number"
                    min="1"
                    max="20000"
                    :disabled="isMockProvider || form.embeddingProviderName === 'mock'"
                  />
                </div>
              </div>

              <div v-if="isOpenAiProvider && !useSharedKeyForEmbedding" class="grid max-w-xl gap-1.5">
                <Label for="ai-embedding-key">Embedding API key</Label>
                <Input
                  id="ai-embedding-key"
                  v-model="form.embeddingApiKey"
                  type="password"
                  autocomplete="new-password"
                  :placeholder="hasSavedEmbeddingApiKey ? 'Để trống nếu vẫn dùng key đã lưu' : 'Dán embedding API key'"
                />
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <div class="mt-4 flex flex-wrap gap-2">
        <Button type="submit" :disabled="isSaving || isLoading || !canSave">
          <SaveIcon class="size-4" />
          {{ isSaving ? 'Đang lưu...' : 'Lưu cấu hình' }}
        </Button>
        <Button variant="outline" type="button" :disabled="isSaving || isTesting || isLoading || !canSave" @click="saveSettings(true)">
          {{ isSaving || isTesting ? 'Đang test...' : 'Lưu và test' }}
        </Button>
        <Button variant="outline" type="button" :disabled="isTesting || isLoading" @click="submitTest">
          <FlaskConicalIcon class="size-4" />
          {{ isTesting ? 'Đang test...' : 'Test cấu hình đã lưu' }}
        </Button>
      </div>
    </form>

    <Card v-if="testResult" class="mt-6">
      <CardContent class="grid gap-2 pt-6 text-sm">
        <div class="flex items-center gap-2">
          <p class="font-medium">Kết quả test</p>
          <StatusBadge
            :status="testResult.success ? 'Approved' : 'Failed'"
            :label="testResult.success ? 'OK' : 'Thất bại'"
          />
        </div>
        <p><span class="text-muted-foreground">Provider:</span> {{ testResult.providerName }}</p>
        <p>{{ testResult.message }}</p>
      </CardContent>
    </Card>

    <p class="mt-6 rounded-md border-l-2 border-warning bg-warning/10 p-3 text-sm text-muted-foreground">
      Nếu các tài liệu cũ được index bằng mock embedding, sau khi chuyển sang OpenAI embedding nên chạy rebuild ở trang
      Chỉ mục tri thức để truy xuất ổn định hơn.
    </p>
  </div>
</template>
