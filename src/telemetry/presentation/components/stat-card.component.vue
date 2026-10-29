<script setup>
const props = defineProps({
  title: {
    type: String,
    required: true
  },
  value: {
    type: [Number, String],
    required: true
  },
  icon: {
    type: String,
    required: true
  },
  iconBgColor: {
    type: String,
    default: 'bg-blue-100'
  },
  iconTextColor: {
    type: String,
    default: 'text-blue-600'
  },
  subtitle: {
    type: String,
    default: ''
  }
});
</script>

<template>
  <div class="stat-card">
    <div class="stat-icon" :class="[iconBgColor, iconTextColor]">
      <i :class="['pi', icon, 'text-2xl']"></i>
    </div>
    <div class="stat-content">
      <p class="stat-label">{{ title }}</p>
      <p class="stat-value">{{ value }}</p>
      <p v-if="subtitle" class="stat-subtitle">{{ subtitle }}</p>
    </div>
  </div>
</template>

<style scoped>
.stat-card {
  background: white;
  border-radius: 0.75rem;
  padding: 1.5rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  transition: transform 0.2s, box-shadow 0.2s;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.stat-icon {
  width: 3.5rem;
  height: 3.5rem;
  border-radius: 0.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.stat-content {
  flex: 1;
  min-width: 0;
}

.stat-label {
  font-size: 0.875rem;
  color: #6B7280;
  margin-bottom: 0.25rem;
  font-weight: 500;
}

.stat-value {
  font-size: 1.75rem;
  font-weight: 700;
  color: #111827;
  line-height: 1.2;
}

.stat-subtitle {
  font-size: 0.75rem;
  color: #9CA3AF;
  margin-top: 0.25rem;
}

/* Icon background color variants (passed as prop strings) */
.bg-blue-100   { background-color: #DBEAFE; }
.bg-green-100  { background-color: #D1FAE5; }
.bg-purple-100 { background-color: #EDE9FE; }
.bg-orange-100 { background-color: #FFEDD5; }
.bg-red-100    { background-color: #FEE2E2; }
.bg-teal-100   { background-color: #CCFBF1; }
.bg-yellow-100 { background-color: #FEF3C7; }

/* Icon text color variants (passed as prop strings) */
.text-blue-600   { color: #2563EB; }
.text-green-600  { color: #16A34A; }
.text-purple-600 { color: #7C3AED; }
.text-orange-600 { color: #EA580C; }
.text-red-600    { color: #DC2626; }
.text-teal-600   { color: #0D9488; }
.text-yellow-600 { color: #D97706; }
</style>
