import { createI18n } from "vue-i18n"
import en from "./locales/en.json"
import es from "./locales/es.json"
import { LANG_KEY } from "./shared/infrastructure/storage-keys.js"

const savedLang = localStorage.getItem(LANG_KEY) || "es"

const i18n = createI18n({
  legacy: false,
  locale: savedLang,
  fallbackLocale: "en",
  messages: { en, es },
})

document.documentElement.lang = savedLang;

export default i18n;
