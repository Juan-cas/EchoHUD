// SPDX-License-Identifier: GPL-3.0-only
//
// Strings.cs - every piece of text the user can read, in one place.
//
// To add a language: copy the English table, translate the values, and add
// it to the switch in PickLanguage(). A test checks that every table has
// exactly the same keys as English, so a missing translation fails the build
// of the test suite instead of showing a blank label.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace EchoHud
{
    internal static class Strings
    {
        static Dictionary<string, string> active;

        // A static constructor runs after the tables below have been built,
        // so it is safe to pick one of them here.
        static Strings()
        {
            active = PickLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }

        /// <summary>Returns the text for a key, or the key itself if it is unknown (so mistakes are visible).</summary>
        internal static string Get(string key)
        {
            string value;
            if (active.TryGetValue(key, out value)) return value;
            return English.TryGetValue(key, out value) ? value : key;
        }

        internal static string Format(string key, params object[] arguments)
        {
            return String.Format(Get(key), arguments);
        }

        internal static Dictionary<string, string> PickLanguage(string twoLetterCode)
        {
            switch (twoLetterCode)
            {
                case "es": return Spanish;
                default: return English;
            }
        }

        /// <summary>Switches language at runtime (used by tests).</summary>
        internal static void Use(string twoLetterCode)
        {
            active = PickLanguage(twoLetterCode);
        }

        internal static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "common.ok", "OK" },
            { "common.cancel", "Cancel" },

            { "source.label", "Source" },
            { "source.demo", "Demo window (practice)" },
            { "source.tibiaMissing", "Tibia (not running)" },
            { "source.obsMissing", "OBS projector (not open)" },

            { "status.live", "{0} connected. Mirrors are live." },
            { "status.waitTibia", "Waiting for Tibia. Start the game or pick another source." },
            { "status.waitObs", "In OBS, right-click the preview and open a Windowed Projector." },
            { "status.waitDemo", "The demo window is closed. Choose it again under Source to reopen it." },
            { "status.minimized", "The source window is minimized. Restore it to see your mirrors." },
            { "status.placed", "Drag the mirror where you want it, then press Lock." },
            { "status.locked", "Locked. Mirrors stay put and clicks pass through them." },
            { "status.unlocked", "Unlocked. Drag mirrors to move them, drag a corner to resize." },
            { "status.limit", "A profile can hold up to {0} regions." },
            { "status.saveFailed", "Could not save: {0}" },
            { "status.loadFailed", "Could not open that profile: {0}" },
            { "status.badName", "Use 1-40 letters, numbers, spaces, - or _ and a name that is not taken." },

            { "action.add", "Add region" },
            { "action.addTip", "Draw a box over the part of the source you want to mirror" },
            { "action.hideAll", "Hide all mirrors" },
            { "action.showAll", "Show all mirrors" },
            { "action.lock", "Lock mirrors in place (clicks pass through)" },
            { "action.unlock", "Unlock mirrors to move and resize them" },
            { "action.about", "About and safety" },

            { "list.empty", "No regions yet.\n\nPress Add region, then draw a box over the part\nof the game you want to see somewhere else." },
            { "list.count", "{0} / {1}" },

            { "row.rename", "Rename" },
            { "row.show", "Show this mirror" },
            { "row.hide", "Hide this mirror" },
            { "row.recrop", "Choose a different area" },
            { "row.delete", "Delete" },
            { "row.size", "Size" },
            { "row.opacity", "Opacity" },
            { "row.defaultName", "Region {0}" },

            { "menu.size", "Size" },
            { "menu.opacity", "Opacity" },
            { "menu.border", "Highlight border when locked" },
            { "menu.recrop", "Choose a different area..." },
            { "menu.rename", "Rename..." },
            { "menu.hide", "Hide" },
            { "menu.delete", "Delete" },

            { "selector.title", "Select area" },
            { "selector.hint", "Click and drag to select an area. Esc to cancel." },

            { "profile.label", "Profile" },
            { "profile.new", "New profile" },
            { "profile.newPrompt", "Name for the new profile" },
            { "profile.delete", "Delete this profile" },
            { "profile.deleteConfirm", "Delete the profile \"{0}\" and all its regions?" },
            { "region.deleteConfirm", "Delete \"{0}\"?" },

            { "help.noSource.tibia", "Tibia was not found.\n\nStart the game and log in. If you play in fullscreen, use the DirectX 11 or DirectX 12 graphics engine: OpenGL uses exclusive fullscreen, which Windows cannot mirror.\n\nNo game? Choose \"Demo window\" as the source to try the app." },
            { "help.noSource.obs", "No OBS projector was found.\n\nIn OBS Studio, right-click the preview (or a scene or source) and choose \"Windowed Projector\". Leave that window open; it may be on another monitor or behind other windows, but not minimized." },
            { "help.noSource.minimized", "The source window is minimized.\n\nWindows cannot mirror a minimized window. Restore it and try again." },

            { "about.text", "Echo HUD {0}\nFree software under the GPL-3.0 license.\n\nWhat it does\nShows live copies of parts of a window in small floating windows, using the Windows thumbnail feature (the same one behind Alt+Tab previews).\n\nWhat it never does\n- read or change game memory or game files\n- inject code or install hooks or drivers\n- send keys or clicks to the game\n- capture or analyse screen pixels\n- connect to the internet\n\nNot affiliated with or approved by CipSoft or BattlEye. Their rules can change; using any third-party tool is at your own risk." }
        };

        internal static readonly Dictionary<string, string> Spanish = new Dictionary<string, string>
        {
            { "common.ok", "Aceptar" },
            { "common.cancel", "Cancelar" },

            { "source.label", "Origen" },
            { "source.demo", "Ventana de práctica (demo)" },
            { "source.tibiaMissing", "Tibia (no está abierto)" },
            { "source.obsMissing", "Proyector de OBS (no está abierto)" },

            { "status.live", "{0} conectado. Los espejos están en vivo." },
            { "status.waitTibia", "Esperando a Tibia. Abre el juego o elige otro origen." },
            { "status.waitObs", "En OBS, clic derecho en la vista previa y abre un Proyector en ventana." },
            { "status.waitDemo", "La ventana de práctica está cerrada. Elígela de nuevo en Origen para reabrirla." },
            { "status.minimized", "La ventana de origen está minimizada. Restáurala para ver tus espejos." },
            { "status.placed", "Arrastra el espejo a su sitio y pulsa Bloquear." },
            { "status.locked", "Bloqueado. Los espejos no se mueven y los clics los atraviesan." },
            { "status.unlocked", "Desbloqueado. Arrastra para mover; arrastra una esquina para cambiar el tamaño." },
            { "status.limit", "Un perfil admite hasta {0} regiones." },
            { "status.saveFailed", "No se pudo guardar: {0}" },
            { "status.loadFailed", "No se pudo abrir ese perfil: {0}" },
            { "status.badName", "Usa de 1 a 40 letras, números, espacios, - o _ y un nombre que no exista." },

            { "action.add", "Añadir región" },
            { "action.addTip", "Dibuja un recuadro sobre la parte del origen que quieres reflejar" },
            { "action.hideAll", "Ocultar todos los espejos" },
            { "action.showAll", "Mostrar todos los espejos" },
            { "action.lock", "Bloquear los espejos (los clics los atraviesan)" },
            { "action.unlock", "Desbloquear los espejos para moverlos y cambiar su tamaño" },
            { "action.about", "Acerca de y seguridad" },

            { "list.empty", "Aún no hay regiones.\n\nPulsa Añadir región y dibuja un recuadro sobre\nla parte del juego que quieres ver en otro sitio." },
            { "list.count", "{0} / {1}" },

            { "row.rename", "Renombrar" },
            { "row.show", "Mostrar este espejo" },
            { "row.hide", "Ocultar este espejo" },
            { "row.recrop", "Elegir otra área" },
            { "row.delete", "Eliminar" },
            { "row.size", "Tamaño" },
            { "row.opacity", "Opacidad" },
            { "row.defaultName", "Región {0}" },

            { "menu.size", "Tamaño" },
            { "menu.opacity", "Opacidad" },
            { "menu.border", "Resaltar el borde al bloquear" },
            { "menu.recrop", "Elegir otra área..." },
            { "menu.rename", "Renombrar..." },
            { "menu.hide", "Ocultar" },
            { "menu.delete", "Eliminar" },

            { "selector.title", "Seleccionar área" },
            { "selector.hint", "Haz clic y arrastra para seleccionar un área. Esc para cancelar." },

            { "profile.label", "Perfil" },
            { "profile.new", "Nuevo perfil" },
            { "profile.newPrompt", "Nombre del nuevo perfil" },
            { "profile.delete", "Eliminar este perfil" },
            { "profile.deleteConfirm", "¿Eliminar el perfil «{0}» y todas sus regiones?" },
            { "region.deleteConfirm", "¿Eliminar «{0}»?" },

            { "help.noSource.tibia", "No se encontró Tibia.\n\nAbre el juego e inicia sesión. Si juegas a pantalla completa, usa el motor gráfico DirectX 11 o DirectX 12: OpenGL usa pantalla completa exclusiva, que Windows no puede reflejar.\n\n¿Sin el juego? Elige «Ventana de práctica» como origen para probar la aplicación." },
            { "help.noSource.obs", "No se encontró ningún proyector de OBS.\n\nEn OBS Studio, haz clic derecho en la vista previa (o en una escena o fuente) y elige «Proyector en ventana». Deja esa ventana abierta; puede estar en otro monitor o detrás de otras ventanas, pero no minimizada." },
            { "help.noSource.minimized", "La ventana de origen está minimizada.\n\nWindows no puede reflejar una ventana minimizada. Restáurala e inténtalo de nuevo." },

            { "about.text", "Echo HUD {0}\nSoftware libre bajo la licencia GPL-3.0.\n\nQué hace\nMuestra copias en vivo de partes de una ventana en pequeñas ventanas flotantes, usando las miniaturas de Windows (las mismas de las vistas previas de Alt+Tab).\n\nQué no hace nunca\n- leer ni modificar la memoria o los archivos del juego\n- inyectar código ni instalar hooks o drivers\n- enviar teclas o clics al juego\n- capturar ni analizar píxeles de la pantalla\n- conectarse a internet\n\nNo está afiliado a CipSoft ni a BattlEye, ni aprobado por ellos. Sus reglas pueden cambiar; usar cualquier herramienta de terceros es bajo tu propia responsabilidad." }
        };
    }
}
