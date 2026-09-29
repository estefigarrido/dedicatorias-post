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
create or replace function public.post_normalizar(t text)
returns text language sql immutable as $$
  select regexp_replace(                                   -- puuuto → puto
           regexp_replace(                                 -- p.u.t.o → puto
             translate(lower(coalesce(t, '')),
               'áéíóúüàèìòùâêîôûñ013457@$',
               'aeiouuaeiouaeiounoieasta' || 's'),
             '([a-z])[.\-_*·,''"]+(?=[a-z])', '\1', 'g'),
           '([a-z])\1+', '\1', 'g');
$$;

create or replace function public.post_moderar()
returns trigger language plpgsql as $$
declare
  prohibidas constant text :=
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
    'fuck|fucking|fucker|motherfucker|shit|bitch|bitches|ashole|dick|cunt|pusy|niger|niga|whore|slut|bastard';
  patron text := '(^|[^a-z])(' || prohibidas || ')($|[^a-z])';
begin
  new.para := btrim(regexp_replace(new.para, '\s+', ' ', 'g'));
  new.mensaje := btrim(regexp_replace(new.mensaje, '\s+', ' ', 'g'));

  if public.post_normalizar(new.para) ~ patron or public.post_normalizar(new.mensaje) ~ patron then
    raise exception 'No está permitido publicar este tipo de contenido.'
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
