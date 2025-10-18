import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

const resources = {
  en: {
    translation: {
      dashboard: "Dashboard",
      users: "Users",
      groups: "Groups",
      ous: "Organizational Units",
      deleted: "Deleted Items",
      office365: "Office 365",
      login: "Login",
      logout: "Logout",
      search: "Search...",
      enable: "Enable",
      disable: "Disable",
      resetPassword: "Reset Password"
    }
  },
  ar: {
    translation: {
      dashboard: "لوحة التحكم",
      users: "المستخدمون",
      groups: "المجموعات",
      ous: "الوحدات التنظيمية",
      deleted: "العناصر المحذوفة",
      office365: "أوفيس 365",
      login: "تسجيل الدخول",
      logout: "تسجيل الخروج",
      search: "ابحث...",
      enable: "تفعيل",
      disable: "تعطيل",
      resetPassword: "إعادة تعيين كلمة المرور"
    }
  }
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'en',
    interpolation: { escapeValue: false }
  });

export default i18n;
