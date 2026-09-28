// Configuración del sitio de dedicatorias post.
// Editá estos valores sin tocar app.js.
window.POST_CONFIG = {
  // Base de datos online (Supabase → Project Settings → API).
  // Con estos dos datos completos, las dedicatorias se guardan en Supabase.
  // La clave "anon / publishable" es pública por diseño: está protegida por las reglas de supabase.sql.
  SUPABASE_URL: 'https://fohbovhqtjppdzghlujf.supabase.co',
  SUPABASE_ANON_KEY: 'sb_publishable_RPH-T9goUD0Q65lX3sjbqg_f0e2Te3t',

  // Solo si NO se usa Supabase: dónde está servidor.ps1. Vacío = el mismo servidor que sirve la página.
  API_URL: '',

  // A dónde lleva "Volver a la app" y la flecha de la primera pantalla.
  // Vacío = vuelve a la página anterior del navegador (si existe).
  APP_URL: '',

  // Modo tablet (abrir con ?modo=tablet): segundos antes de volver solo al inicio
  // después de publicar.
  TABLET_RESET_SEG: 20,
};
