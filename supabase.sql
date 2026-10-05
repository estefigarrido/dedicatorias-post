-- ============================================================
--  Dedicatorias post. — base de datos en Supabase
--  Pegar TODO en Supabase → SQL Editor → New query → Run.
--  Se puede volver a correr sin romper nada.
-- ============================================================

-- 1) Tabla -----------------------------------------------------
create table if not exists public.notas (
  id       bigint generated always as identity primary key,
  para     text not null check (char_length(para) between 1 and 20),
  mensaje  text not null,
  color    text not null default 'verde',
  -- Tamaño de la notita (tarjetita1..4 del Figma), según el largo del mensaje.
  tamano   text generated always as (
             case when char_length(mensaje) <= 40  then 'S'
                  when char_length(mensaje) <= 120 then 'M'
                  when char_length(mensaje) <= 170 then 'L'
                  else 'XL' end) stored,
  visible  boolean not null default true,   -- moderación: poner en false para sacarla de pantalla
  creada   timestamptz not null default now()
);

-- Reglas de largo y colores (se actualizan también en una tabla que ya existía).
-- "not valid": las notas viejas de hasta 250 caracteres quedan como están; las nuevas, máximo 150.
alter table public.notas drop constraint if exists notas_mensaje_check;
alter table public.notas add constraint notas_mensaje_check
  check (char_length(mensaje) between 3 and 150) not valid;
alter table public.notas drop constraint if exists notas_color_check;
alter table public.notas add constraint notas_color_check
  check (color in ('azul', 'verde', 'violeta', 'rosa', 'crema'));

-- Color en hex, calculado desde el nombre.
alter table public.notas drop column if exists hex;
alter table public.notas add column hex text generated always as (
  case color when 'azul' then '#7aa1ff' when 'verde' then '#49b867'
             when 'violeta' then '#ab8ae6' when 'crema' then '#ede8db' else '#f79ee1' end) stored;

-- 2) Filtro de moderación (mismo criterio que moderacion.js) --------
-- Cómo suena: k/q → c, v → b, z → s, y → i, x → ch, sin letras dobles. Se aplica igual al texto
-- y a las listas de palabras.
create or replace function public.post_plegar(t text)
returns text language sql immutable as $$
  select regexp_replace(replace(translate(t, 'kqvzy', 'ccbsi'), 'x', 'ch'), '([a-z])\1+', '\1', 'g');
$$;

-- Minúsculas, sin acentos ni letras de otros alfabetos que se ven iguales, números y símbolos
-- en lugar de letras ("p3lotudo", "put@", "p!ja"), sin separadores ("p.u.t.o", "p u t o") ni
-- letras repetidas ("puuuto"), y plegado.
create or replace function public.post_normalizar(t text)
returns text language plpgsql immutable as $$
declare
  s text := lower(normalize(coalesce(t, ''), NFKC));
begin
  s := regexp_replace(s, '[​-‏⁠﻿­]', '', 'g');
  s := translate(s,
    'áéíóúüàèìòùâêîôûñäëïöçаеорсухіјѕԁοαρτυνκιεɡß013456789@ª$§€',
    'aeiouuaeiouaeiounaeiocaeopcyxijsdoaptuvkiegboieasgtbgaasse');
  s := regexp_replace(s, '([a-z])[!|¡](?=[a-z])', '\1i', 'g');
  s := regexp_replace(s, '([a-z])\+(?=[a-z])', '\1t', 'g');
  s := regexp_replace(s, '([a-z])[^a-z0-9[:space:]]+(?=[a-z])', '\1', 'g');   -- p.u.t.o → puto
  s := regexp_replace(s, '\y([a-z]) (?=[a-z]\y)', '\1', 'g');                  -- p u t o → puto
  s := regexp_replace(s, '([a-z])\1+', '\1', 'g');                             -- puuuto → puto
  return public.post_plegar(s);
end;
$$;

-- ¿Es ofensivo? Palabras prohibidas (también disfrazadas) o insultos por el contexto: despectivas
-- dirigidas a alguien ("sos un inútil") y frases que agreden ("ojalá te mueras", "gracias por nada").
create or replace function public.post_ofensivo(texto text)
returns boolean language plpgsql immutable as $$
declare
  t text := public.post_normalizar(texto);
  frase text := btrim(regexp_replace(t, '[^a-z]+', ' ', 'g'));
  prohibidas text := public.post_plegar(
    -- insultos
    'put[oa]s?|putit[oa]s?|putaz[oa]s?|hij[oa]s? de put[oa]|hdp|hdep|lpm|lptm|' ||
    'pelotud[oa]s?|bolud[oa]s?|for[oa]s?|conchud[oa]s?|la concha|concha de (tu|su)|' ||
    'cul[ie]ad[oa]s?|mierdas?|sorete?s?|cagon(a|es)?|garcas?|' ||
    'idiotas?|imbecil(es)?|estupid[oa]s?|tarad[oa]s?|mogolic[oa]s?|retrasad[oa]s?|' ||
    'retardad[oa]s?|pajer[oa]s?|chupala|chupame|chupa ?pija|mamaguev[oa]|malparid[oa]s?|' ||
    'gonorea|cabron(a|es)?|pendej[oa]s?|zoras?|pera de mierda|' ||
    -- discriminación
    'trol[oa]s?|marica|maricon(es)?|tortiler[oa]s?|negr[oa]s? de mierda|sudacas?|' ||
    'vilero?s?|vilera?s?|nazis?|hitler|' ||
    -- sexual
    'pijas?|pijud[oa]|porongas?|vergas?|garcha|garchar|coger|cogerte|cogida|pete|peter[oa]|' ||
    'orto|ojete|culos?|tetas?|pene|vagina|porno|sexo|prostitut[oa]s?|chot[oa]|paja|' ||
    -- drogas
    'falopa|merca|faso|' ||
    -- inglés
    'fuck|fucking|fucker|motherfucker|shit|bitch|bitches|ashole|dick|cunt|pusy|niger|niga|whore|slut|bastard');
  juntas text := public.post_plegar(
    'pelotud|conchud|imbecil|mogolic|retrasad|retardad|malparid|prostitut|motherfucker|hijodeput');
  desp text := public.post_plegar(
    'inutil(es)?|basuras?|lacras?|ratas?|escoria|cerd[oa]s?|asqueros[oa]s?|' ||
    'fracasad[oa]s?|perdedor(a|es)?|mediocres?|patetic[oa]s?|ridicul[oa]s?|payas[oa]s?|' ||
    'tont[oa]s?|bob[oa]s?|gil(es)?|salames?|nab[oa]s?|chantas?|mentiros[oa]s?|' ||
    'traidor(a|es)?|cobardes?|vag[oa]s?|fe[oa]s?|horribles?|despreciables?|miserables?|' ||
    'infeliz|infelices|ignorantes?|egoistas?|envidios[oa]s?|resentid[oa]s?|chor[oa]s?|' ||
    'ladron(a|es)?|falsa?o?s?|loser|stupid|idiot|dumb|ugly');
  relleno text := public.post_plegar('((un|una|unos|unas|el|la|los|las|re|muy|tan|mas|alto|alta|flor de|' ||
    'tremend[oa]|terrible|medio|media|bastante|bien|tal|un pedazo de|siempre|solo|nada mas que|tremenda|gran|menudo) ){0,3}');
  dirigidas text[] := array[
    public.post_plegar('(sos|seas|eres|fuiste|seguis siendo|siempre fuiste|pareces|quedaste como|te ves) ') || relleno || '(' || desp || ')',
    public.post_plegar('(es|son|sea|sean|fue|era|eran) ((re|muy|tan|mas|alto|alta|tremend[oa]|terrible|flor de) )?(un|una|unos|unas) ') || relleno || '(' || desp || ')',
    public.post_plegar('que ((re|tan|mas|gran) )?') || '(' || desp || ')' || public.post_plegar(' (que )?(sos|eres)'),
    '^' || public.post_plegar('((che|ei|eh|vos|para|a la|al|el|la|sos|un|una|re|muy|tal) ){0,3}') || '(' || desp || ')'
      || public.post_plegar('( (total|absolut[oa]|de persona|humana?o?))?') || '$'];
  agresiones text := public.post_plegar(
    'ojala (que )?(te|se) (mueras?|muera|mueran|pudras?|pudra|lesiones|caigas|pise un auto)|' ||
    'ojala (que )?(pierdas|nunca llegues|abandones)|' ||
    'te (voy|vamos) a (matar|cagar a palos|romper la cara|fajar)|' ||
    'and(a|ate) a (morir|cagar)|morite|muerete|matate|suicidate|pegate un tiro|' ||
    'desaparece de mi vida|deja de existir|no deberias existir|ni deberias existir|' ||
    'nadie te (quiere|soporta|banca|aguanta|extrana|necesita)|nadie los (quiere|soporta|banca)|' ||
    'no (servis|sirves|vales|sabes) (para|ni) nada|no (servis|sirves|vales) nada|no sabes hacer nada|' ||
    '(me |nos )?(das|dabas) (asco|pena|verguenza|lastima|bronca|vomito)|' ||
    'te (odio|detesto|desprecio|aborrezco)|(los|las) (odio|detesto|desprecio)|' ||
    'sos lo peor|lo peor que me paso|me arruinaste la vida|arruinaste mi vida|' ||
    'gracias por nada|te deseo lo peor|les deseo lo peor|ojala te vaya mal|' ||
    'que te (pise|parta|coja|atropelle)|cerra el pico|nunca te voy a perdonar');
  elogios text := public.post_plegar('(ratas? de (gimnasio|gym|biblioteca|pista)|rata de calle)');
  sin_elogios text;
  d text;
begin
  if t ~ ('(^|[^a-z])(' || prohibidas || ')($|[^a-z])')
     or frase ~ ('(^|[^a-z])(' || prohibidas || ')($|[^a-z])')
     or replace(frase, ' ', '') ~ juntas then
    return true;
  end if;
  sin_elogios := btrim(regexp_replace(regexp_replace(frase, '(^|[^a-z])' || elogios || '($|[^a-z])', ' ', 'g'), ' +', ' ', 'g'));
  foreach d in array dirigidas loop
    if sin_elogios ~ ('(^|[^a-z])' || d || '($|[^a-z])') then return true; end if;
  end loop;
  return frase ~ ('(^|[^a-z])(' || agresiones || ')($|[^a-z])');
end;
$$;

create or replace function public.post_moderar()
returns trigger language plpgsql as $$
begin
  new.para := btrim(regexp_replace(new.para, '\s+', ' ', 'g'));
  new.mensaje := btrim(regexp_replace(new.mensaje, '\s+', ' ', 'g'));

  if public.post_ofensivo(new.para) or public.post_ofensivo(new.mensaje) then
    raise exception 'Ese tipo de insultos o vocabulario no está permitido.'
      using errcode = 'P0001', detail = 'ofensivo';
  end if;
  if (new.para || ' ' || new.mensaje) ~* '(https?://|www\.|[a-z0-9-]+\.(com|ar|net|org|io|me|ly|gg|tv|app)\y)' then
    raise exception 'No se pueden incluir links.' using errcode = 'P0001', detail = 'link';
  end if;
  if (new.para || ' ' || new.mensaje) ~ '\d[\d\s.-]{7,}\d' then
    raise exception 'No se pueden incluir números de teléfono.' using errcode = 'P0001', detail = 'datos';
  end if;

  -- Freno anti-spam: como máximo 20 notas por minuto en total.
  if (select count(*) from public.notas where creada > now() - interval '1 minute') >= 20 then
    raise exception 'Hay muchas dedicatorias entrando. Probá de nuevo en un minuto.'
      using errcode = 'P0001', detail = 'espera';
  end if;

  new.visible := true;
  new.creada := now();
  return new;
end;
$$;

drop trigger if exists notas_moderar on public.notas;
create trigger notas_moderar before insert on public.notas
  for each row execute function public.post_moderar();

-- 3) Permisos (Row Level Security) ---------------------------------
-- El sitio y Unity usan la clave pública (anon): pueden crear notas y leer las visibles.
-- No pueden editar ni borrar. La moderación se hace desde el panel de Supabase.
alter table public.notas enable row level security;

drop policy if exists "crear notas" on public.notas;
create policy "crear notas" on public.notas
  for insert to anon with check (true);

drop policy if exists "leer notas visibles" on public.notas;
create policy "leer notas visibles" on public.notas
  for select to anon using (visible);

grant usage on schema public to anon;
grant select, insert on public.notas to anon;
revoke update, delete on public.notas from anon;
