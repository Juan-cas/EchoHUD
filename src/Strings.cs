// SPDX-License-Identifier: GPL-3.0-only
//
// Strings.cs - every piece of text the user can read, in one place.
//
// To add a language: copy the English table, translate the values, add it
// to the switch in PickLanguage() and to All, and add its code to
// Settings.Languages (Model.cs) and its name to the language menu
// (MainForm.ShowSettingsMenu). A test checks that every table has exactly
// the same keys and placeholders as English, so a missing translation fails
// the test suite instead of showing a blank label.
//
// The Portuguese and Polish texts were not written by native speakers.
// Corrections are very welcome - this file is all you need to touch.

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
                case "pt": return Portuguese;
                case "pl": return Polish;
                default: return English;
            }
        }

        /// <summary>
        /// Switches language. An empty code means "the language of Windows".
        /// Call before any window is built: texts already on screen do not change.
        /// </summary>
        internal static void Use(string twoLetterCode)
        {
            active = PickLanguage(String.IsNullOrEmpty(twoLetterCode) ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : twoLetterCode);
        }

        /// <summary>Every translation except English, by language code (used by tests).</summary>
        internal static Dictionary<string, Dictionary<string, string>> All
        {
            get
            {
                return new Dictionary<string, Dictionary<string, string>> { { "es", Spanish }, { "pt", Portuguese }, { "pl", Polish } };
            }
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
            { "status.placed", "Drag the mirror into place. Arrow keys fine-tune the area (Shift resizes). Then Lock." },
            { "status.locked", "Locked. Mirrors stay put and clicks pass through them." },
            { "status.unlocked", "Unlocked. Drag to move: Shift moves the group, Ctrl moves everything. Arrow keys fine-tune." },
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

            { "selector.hintMany", "Click and drag to select an area. Hold Shift to select several. Esc to cancel." },
            { "selector.progress", "{0} selected. Shift+drag another, press R to repeat the spacing, or Enter to finish." },
            { "status.placedMany", "Mirrors added as a group. Hold Shift while dragging one to move the group." },
            { "status.timerAdded", "Timer added. Set its duration and hotkey, drag it into place, then Lock." },
            { "status.timerLimit", "A profile can hold up to {0} timers." },
            { "status.hotkeyTaken", "Windows refused this hotkey (already in use?): {0}" },
            { "status.badDuration", "Type a duration such as 10 (minutes), 10:30 or 1:00:00." },
            { "action.addTimer", "Add a cooldown timer" },

            { "timer.ready", "Ready" },
            { "timer.start", "Start / restart" },
            { "timer.reset", "Reset" },
            { "timer.duration", "Duration..." },
            { "timer.hotkey", "Hotkey..." },
            { "timer.sound", "Sound when ready" },
            { "timer.startTip", "Start or restart this timer" },
            { "timer.durationTip", "Change the duration" },
            { "timer.hotkeyTip", "Choose the key that starts this timer" },
            { "timer.defaultName", "Timer {0}" },
            { "timer.durationPrompt", "Duration (minutes, or minutes:seconds)" },

            { "hotkey.title", "Timer hotkey" },
            { "hotkey.explain", "Press the key combination that should start this timer.\n\nWhile Echo HUD is running, Windows gives this combination to Echo HUD and NOT to the game. Pick one you do not use in game, for example Ctrl+F5, and press it right after you use the item." },
            { "hotkey.none", "No hotkey" },
            { "hotkey.clear", "Remove" },

            { "settings.title", "Settings" },
            { "settings.onlyWithGame", "Show mirrors only while the game is in front" },
            { "settings.snap", "Snap mirrors to each other" },
            { "settings.tray", "Minimize to the notification area" },
            { "settings.autostart", "Start with Windows" },
            { "settings.lockHotkey", "Hotkey to lock / unlock: {0}" },
            { "settings.showHotkey", "Hotkey to show / hide: {0}" },
            { "settings.lockHotkeyTitle", "Lock / unlock hotkey" },
            { "settings.showHotkeyTitle", "Show / hide hotkey" },
            { "settings.language", "Language" },
            { "settings.languageAuto", "Same as Windows" },
            { "status.languageNext", "The language changes the next time you start Echo HUD." },
            { "hotkey.explainGlobal", "Press the key combination to use.\n\nWhile Echo HUD is running, Windows gives this combination to Echo HUD and NOT to the game. Pick one you do not use in game, for example Ctrl+F6." },

            { "profile.import", "Import a profile..." },
            { "profile.export", "Export this profile..." },
            { "profile.fileType", "Echo HUD profile" },
            { "status.exported", "Profile exported. Sound file locations are not included." },
            { "status.imported", "Imported as \"{0}\"." },
            { "action.releases", "Look for a new version (opens the website)" },

            { "tray.open", "Open Echo HUD" },
            { "tray.lock", "Lock mirrors" },
            { "tray.unlock", "Unlock mirrors" },
            { "tray.exit", "Exit" },
            { "tray.hint", "Echo HUD is still running. Click this icon to open it again." },

            { "menu.group", "Group" },
            { "group.none", "No group" },
            { "group.new", "New group..." },
            { "group.newPrompt", "Name for the new group" },
            { "group.defaultName", "Group {0}" },
            { "status.badGroup", "A group name needs 1 to 40 characters." },
            { "selector.noRoom", "There is no room for another area in that direction." },

            { "timer.minutes", "{0} min" },
            { "timer.durationOther", "Other..." },
            { "timer.shape", "Shape" },
            { "timer.shapeCircle", "Circle" },
            { "timer.shapeBar", "Bar" },
            { "timer.colour", "Colour" },
            { "timer.soundOff", "No sound" },
            { "timer.soundStandard", "Standard sound" },
            { "timer.soundFile", "Choose a sound file (.wav)..." },
            { "timer.countUp", "Count up after it is ready" },

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
            { "status.placed", "Arrastra el espejo a su sitio. Las flechas ajustan el área (Mayús cambia el tamaño). Luego Bloquear." },
            { "status.locked", "Bloqueado. Los espejos no se mueven y los clics los atraviesan." },
            { "status.unlocked", "Desbloqueado. Arrastra para mover: Mayús mueve el grupo, Ctrl mueve todo. Las flechas ajustan." },
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

            { "selector.hintMany", "Haz clic y arrastra para seleccionar un área. Mantén Mayús para seleccionar varias. Esc para cancelar." },
            { "selector.progress", "{0} seleccionadas. Mayús+arrastrar otra, R repite el espaciado, Intro termina." },
            { "status.placedMany", "Espejos añadidos como grupo. Mantén Mayús al arrastrar uno para mover el grupo." },
            { "status.timerAdded", "Temporizador añadido. Ajusta su duración y su tecla, colócalo y pulsa Bloquear." },
            { "status.timerLimit", "Un perfil admite hasta {0} temporizadores." },
            { "status.hotkeyTaken", "Windows rechazó esta tecla (¿ya está en uso?): {0}" },
            { "status.badDuration", "Escribe una duración como 10 (minutos), 10:30 o 1:00:00." },
            { "action.addTimer", "Añadir un temporizador de enfriamiento" },

            { "timer.ready", "Listo" },
            { "timer.start", "Iniciar / reiniciar" },
            { "timer.reset", "Restablecer" },
            { "timer.duration", "Duración..." },
            { "timer.hotkey", "Tecla..." },
            { "timer.sound", "Sonido al terminar" },
            { "timer.startTip", "Iniciar o reiniciar este temporizador" },
            { "timer.durationTip", "Cambiar la duración" },
            { "timer.hotkeyTip", "Elegir la tecla que inicia este temporizador" },
            { "timer.defaultName", "Temporizador {0}" },
            { "timer.durationPrompt", "Duración (minutos, o minutos:segundos)" },

            { "hotkey.title", "Tecla del temporizador" },
            { "hotkey.explain", "Pulsa la combinación de teclas que debe iniciar este temporizador.\n\nMientras Echo HUD esté abierto, Windows entrega esta combinación a Echo HUD y NO al juego. Elige una que no uses en el juego, por ejemplo Ctrl+F5, y púlsala justo después de usar el objeto." },
            { "hotkey.none", "Sin tecla" },
            { "hotkey.clear", "Quitar" },

            { "settings.title", "Ajustes" },
            { "settings.onlyWithGame", "Mostrar los espejos solo con el juego en primer plano" },
            { "settings.snap", "Ajustar los espejos entre sí" },
            { "settings.tray", "Minimizar al área de notificación" },
            { "settings.autostart", "Iniciar con Windows" },
            { "settings.lockHotkey", "Tecla para bloquear / desbloquear: {0}" },
            { "settings.showHotkey", "Tecla para mostrar / ocultar: {0}" },
            { "settings.lockHotkeyTitle", "Tecla de bloquear / desbloquear" },
            { "settings.showHotkeyTitle", "Tecla de mostrar / ocultar" },
            { "settings.language", "Idioma" },
            { "settings.languageAuto", "El de Windows" },
            { "status.languageNext", "El idioma cambiará la próxima vez que abras Echo HUD." },
            { "hotkey.explainGlobal", "Pulsa la combinación de teclas que quieres usar.\n\nMientras Echo HUD esté abierto, Windows entrega esta combinación a Echo HUD y NO al juego. Elige una que no uses en el juego, por ejemplo Ctrl+F6." },

            { "profile.import", "Importar un perfil..." },
            { "profile.export", "Exportar este perfil..." },
            { "profile.fileType", "Perfil de Echo HUD" },
            { "status.exported", "Perfil exportado. No incluye la ubicación de los archivos de sonido." },
            { "status.imported", "Importado como «{0}»." },
            { "action.releases", "Buscar una versión nueva (abre la web)" },

            { "tray.open", "Abrir Echo HUD" },
            { "tray.lock", "Bloquear los espejos" },
            { "tray.unlock", "Desbloquear los espejos" },
            { "tray.exit", "Salir" },
            { "tray.hint", "Echo HUD sigue abierto. Haz clic en este icono para volver a verlo." },

            { "menu.group", "Grupo" },
            { "group.none", "Sin grupo" },
            { "group.new", "Nuevo grupo..." },
            { "group.newPrompt", "Nombre del nuevo grupo" },
            { "group.defaultName", "Grupo {0}" },
            { "status.badGroup", "El nombre del grupo debe tener de 1 a 40 caracteres." },
            { "selector.noRoom", "No queda sitio para otra área en esa dirección." },

            { "timer.minutes", "{0} min" },
            { "timer.durationOther", "Otra..." },
            { "timer.shape", "Forma" },
            { "timer.shapeCircle", "Círculo" },
            { "timer.shapeBar", "Barra" },
            { "timer.colour", "Color" },
            { "timer.soundOff", "Sin sonido" },
            { "timer.soundStandard", "Sonido estándar" },
            { "timer.soundFile", "Elegir un archivo de sonido (.wav)..." },
            { "timer.countUp", "Seguir contando al terminar" },

            { "about.text", "Echo HUD {0}\nSoftware libre bajo la licencia GPL-3.0.\n\nQué hace\nMuestra copias en vivo de partes de una ventana en pequeñas ventanas flotantes, usando las miniaturas de Windows (las mismas de las vistas previas de Alt+Tab).\n\nQué no hace nunca\n- leer ni modificar la memoria o los archivos del juego\n- inyectar código ni instalar hooks o drivers\n- enviar teclas o clics al juego\n- capturar ni analizar píxeles de la pantalla\n- conectarse a internet\n\nNo está afiliado a CipSoft ni a BattlEye, ni aprobado por ellos. Sus reglas pueden cambiar; usar cualquier herramienta de terceros es bajo tu propia responsabilidad." }
        };
    

        internal static readonly Dictionary<string, string> Portuguese = new Dictionary<string, string>
        {
            { "common.ok", "OK" },
            { "common.cancel", "Cancelar" },

            { "source.label", "Origem" },
            { "source.demo", "Janela de treino (demo)" },
            { "source.tibiaMissing", "Tibia (não está aberto)" },
            { "source.obsMissing", "Projetor do OBS (não está aberto)" },

            { "status.live", "{0} conectado. Os espelhos estão ao vivo." },
            { "status.waitTibia", "Aguardando o Tibia. Abra o jogo ou escolha outra origem." },
            { "status.waitObs", "No OBS, clique com o botão direito na prévia e abra um Projetor em janela." },
            { "status.waitDemo", "A janela de treino está fechada. Escolha-a de novo em Origem para reabri-la." },
            { "status.minimized", "A janela de origem está minimizada. Restaure-a para ver seus espelhos." },
            { "status.placed", "Arraste o espelho para o lugar. As setas ajustam a área (Shift muda o tamanho). Depois, Travar." },
            { "status.locked", "Travado. Os espelhos ficam no lugar e os cliques passam por eles." },
            { "status.unlocked", "Destravado. Arraste para mover: Shift move o grupo, Ctrl move tudo. As setas ajustam." },
            { "status.limit", "Um perfil comporta até {0} regiões." },
            { "status.saveFailed", "Não foi possível salvar: {0}" },
            { "status.loadFailed", "Não foi possível abrir esse perfil: {0}" },
            { "status.badName", "Use de 1 a 40 letras, números, espaços, - ou _ e um nome que ainda não exista." },

            { "action.add", "Adicionar região" },
            { "action.addTip", "Desenhe um retângulo sobre a parte da origem que você quer espelhar" },
            { "action.hideAll", "Ocultar todos os espelhos" },
            { "action.showAll", "Mostrar todos os espelhos" },
            { "action.lock", "Travar os espelhos (os cliques passam por eles)" },
            { "action.unlock", "Destravar os espelhos para mover e redimensionar" },
            { "action.about", "Sobre e segurança" },

            { "list.empty", "Nenhuma região ainda.\n\nClique em Adicionar região e desenhe um retângulo\nsobre a parte do jogo que você quer ver em outro lugar." },
            { "list.count", "{0} / {1}" },

            { "row.rename", "Renomear" },
            { "row.show", "Mostrar este espelho" },
            { "row.hide", "Ocultar este espelho" },
            { "row.recrop", "Escolher outra área" },
            { "row.delete", "Excluir" },
            { "row.size", "Tamanho" },
            { "row.opacity", "Opacidade" },
            { "row.defaultName", "Região {0}" },

            { "menu.size", "Tamanho" },
            { "menu.opacity", "Opacidade" },
            { "menu.border", "Destacar a borda quando travado" },
            { "menu.recrop", "Escolher outra área..." },
            { "menu.rename", "Renomear..." },
            { "menu.hide", "Ocultar" },
            { "menu.delete", "Excluir" },

            { "selector.title", "Selecionar área" },
            { "selector.hint", "Clique e arraste para selecionar uma área. Esc para cancelar." },

            { "profile.label", "Perfil" },
            { "profile.new", "Novo perfil" },
            { "profile.newPrompt", "Nome do novo perfil" },
            { "profile.delete", "Excluir este perfil" },
            { "profile.deleteConfirm", "Excluir o perfil \"{0}\" e todas as suas regiões?" },
            { "region.deleteConfirm", "Excluir \"{0}\"?" },

            { "help.noSource.tibia", "O Tibia não foi encontrado.\n\nAbra o jogo e entre com seu personagem. Se você joga em tela cheia, use o motor gráfico DirectX 11 ou DirectX 12: o OpenGL usa tela cheia exclusiva, que o Windows não consegue espelhar.\n\nSem o jogo? Escolha \"Janela de treino\" como origem para experimentar o programa." },
            { "help.noSource.obs", "Nenhum projetor do OBS foi encontrado.\n\nNo OBS Studio, clique com o botão direito na prévia (ou em uma cena ou fonte) e escolha \"Projetor em janela\". Deixe essa janela aberta; ela pode ficar em outro monitor ou atrás de outras janelas, mas não minimizada." },
            { "help.noSource.minimized", "A janela de origem está minimizada.\n\nO Windows não consegue espelhar uma janela minimizada. Restaure-a e tente de novo." },

            { "selector.hintMany", "Clique e arraste para selecionar uma área. Segure Shift para selecionar várias. Esc para cancelar." },
            { "selector.progress", "{0} selecionadas. Shift+arrastar outra, R repete o espaçamento, Enter termina." },
            { "status.placedMany", "Espelhos adicionados como grupo. Segure Shift ao arrastar um para mover o grupo." },
            { "status.timerAdded", "Temporizador adicionado. Defina a duração e a tecla, arraste para o lugar e trave." },
            { "status.timerLimit", "Um perfil comporta até {0} temporizadores." },
            { "status.hotkeyTaken", "O Windows recusou esta tecla (já está em uso?): {0}" },
            { "status.badDuration", "Digite uma duração como 10 (minutos), 10:30 ou 1:00:00." },
            { "action.addTimer", "Adicionar um temporizador de cooldown" },

            { "timer.ready", "Pronto" },
            { "timer.start", "Iniciar / reiniciar" },
            { "timer.reset", "Zerar" },
            { "timer.duration", "Duração..." },
            { "timer.hotkey", "Tecla..." },
            { "timer.sound", "Som ao terminar" },
            { "timer.startTip", "Iniciar ou reiniciar este temporizador" },
            { "timer.durationTip", "Mudar a duração" },
            { "timer.hotkeyTip", "Escolher a tecla que inicia este temporizador" },
            { "timer.defaultName", "Temporizador {0}" },
            { "timer.durationPrompt", "Duração (minutos, ou minutos:segundos)" },

            { "hotkey.title", "Tecla do temporizador" },
            { "hotkey.explain", "Pressione a combinação de teclas que deve iniciar este temporizador.\n\nEnquanto o Echo HUD estiver aberto, o Windows entrega esta combinação ao Echo HUD e NÃO ao jogo. Escolha uma que você não usa no jogo, por exemplo Ctrl+F5, e pressione-a logo depois de usar o item." },
            { "hotkey.none", "Sem tecla" },
            { "hotkey.clear", "Remover" },

            { "settings.title", "Configurações" },
            { "settings.onlyWithGame", "Mostrar os espelhos só com o jogo em primeiro plano" },
            { "settings.snap", "Encaixar os espelhos uns nos outros" },
            { "settings.tray", "Minimizar para a área de notificação" },
            { "settings.autostart", "Iniciar com o Windows" },
            { "settings.lockHotkey", "Tecla para travar / destravar: {0}" },
            { "settings.showHotkey", "Tecla para mostrar / ocultar: {0}" },
            { "settings.lockHotkeyTitle", "Tecla de travar / destravar" },
            { "settings.showHotkeyTitle", "Tecla de mostrar / ocultar" },
            { "settings.language", "Idioma" },
            { "settings.languageAuto", "O mesmo do Windows" },
            { "status.languageNext", "O idioma muda na próxima vez que você abrir o Echo HUD." },
            { "hotkey.explainGlobal", "Pressione a combinação de teclas que você quer usar.\n\nEnquanto o Echo HUD estiver aberto, o Windows entrega esta combinação ao Echo HUD e NÃO ao jogo. Escolha uma que você não usa no jogo, por exemplo Ctrl+F6." },

            { "profile.import", "Importar um perfil..." },
            { "profile.export", "Exportar este perfil..." },
            { "profile.fileType", "Perfil do Echo HUD" },
            { "status.exported", "Perfil exportado. O local dos arquivos de som não é incluído." },
            { "status.imported", "Importado como \"{0}\"." },
            { "action.releases", "Procurar uma versão nova (abre o site)" },

            { "tray.open", "Abrir o Echo HUD" },
            { "tray.lock", "Travar os espelhos" },
            { "tray.unlock", "Destravar os espelhos" },
            { "tray.exit", "Sair" },
            { "tray.hint", "O Echo HUD continua aberto. Clique neste ícone para vê-lo de novo." },

            { "menu.group", "Grupo" },
            { "group.none", "Sem grupo" },
            { "group.new", "Novo grupo..." },
            { "group.newPrompt", "Nome do novo grupo" },
            { "group.defaultName", "Grupo {0}" },
            { "status.badGroup", "O nome do grupo precisa ter de 1 a 40 caracteres." },
            { "selector.noRoom", "Não há espaço para outra área nessa direção." },

            { "timer.minutes", "{0} min" },
            { "timer.durationOther", "Outra..." },
            { "timer.shape", "Forma" },
            { "timer.shapeCircle", "Círculo" },
            { "timer.shapeBar", "Barra" },
            { "timer.colour", "Cor" },
            { "timer.soundOff", "Sem som" },
            { "timer.soundStandard", "Som padrão" },
            { "timer.soundFile", "Escolher um arquivo de som (.wav)..." },
            { "timer.countUp", "Continuar contando depois de pronto" },

            { "about.text", "Echo HUD {0}\nSoftware livre sob a licença GPL-3.0.\n\nO que ele faz\nMostra cópias ao vivo de partes de uma janela em pequenas janelas flutuantes, usando as miniaturas do Windows (as mesmas das prévias do Alt+Tab).\n\nO que ele nunca faz\n- ler ou alterar a memória ou os arquivos do jogo\n- injetar código ou instalar hooks ou drivers\n- enviar teclas ou cliques ao jogo\n- capturar ou analisar pixels da tela\n- conectar-se à internet\n\nNão é afiliado à CipSoft nem à BattlEye, nem aprovado por elas. As regras delas podem mudar; usar qualquer ferramenta de terceiros é por sua conta e risco." }
        };

        internal static readonly Dictionary<string, string> Polish = new Dictionary<string, string>
        {
            { "common.ok", "OK" },
            { "common.cancel", "Anuluj" },

            { "source.label", "Źródło" },
            { "source.demo", "Okno ćwiczebne (demo)" },
            { "source.tibiaMissing", "Tibia (nie jest uruchomiona)" },
            { "source.obsMissing", "Projektor OBS (nie jest otwarty)" },

            { "status.live", "{0} połączone. Lustra działają na żywo." },
            { "status.waitTibia", "Czekam na Tibię. Uruchom grę albo wybierz inne źródło." },
            { "status.waitObs", "W OBS kliknij podgląd prawym przyciskiem i otwórz Projektor w oknie." },
            { "status.waitDemo", "Okno ćwiczebne jest zamknięte. Wybierz je ponownie w polu Źródło, aby je otworzyć." },
            { "status.minimized", "Okno źródłowe jest zminimalizowane. Przywróć je, aby zobaczyć lustra." },
            { "status.placed", "Przeciągnij lustro na miejsce. Strzałki dopasowują obszar (Shift zmienia rozmiar). Potem Zablokuj." },
            { "status.locked", "Zablokowane. Lustra stoją w miejscu, a kliknięcia przez nie przechodzą." },
            { "status.unlocked", "Odblokowane. Przeciągaj: Shift przesuwa grupę, Ctrl wszystko. Strzałki dopasowują." },
            { "status.limit", "Profil mieści do {0} regionów." },
            { "status.saveFailed", "Nie udało się zapisać: {0}" },
            { "status.loadFailed", "Nie udało się otworzyć tego profilu: {0}" },
            { "status.badName", "Użyj 1-40 liter, cyfr, spacji, - lub _ oraz nazwy, która nie jest zajęta." },

            { "action.add", "Dodaj region" },
            { "action.addTip", "Narysuj ramkę na części źródła, którą chcesz odbić" },
            { "action.hideAll", "Ukryj wszystkie lustra" },
            { "action.showAll", "Pokaż wszystkie lustra" },
            { "action.lock", "Zablokuj lustra (kliknięcia przez nie przechodzą)" },
            { "action.unlock", "Odblokuj lustra, aby je przesuwać i zmieniać ich rozmiar" },
            { "action.about", "O programie i bezpieczeństwo" },

            { "list.empty", "Nie ma jeszcze regionów.\n\nKliknij Dodaj region i narysuj ramkę na części gry,\nktórą chcesz widzieć w innym miejscu." },
            { "list.count", "{0} / {1}" },

            { "row.rename", "Zmień nazwę" },
            { "row.show", "Pokaż to lustro" },
            { "row.hide", "Ukryj to lustro" },
            { "row.recrop", "Wybierz inny obszar" },
            { "row.delete", "Usuń" },
            { "row.size", "Rozmiar" },
            { "row.opacity", "Krycie" },
            { "row.defaultName", "Region {0}" },

            { "menu.size", "Rozmiar" },
            { "menu.opacity", "Krycie" },
            { "menu.border", "Wyróżnij ramkę po zablokowaniu" },
            { "menu.recrop", "Wybierz inny obszar..." },
            { "menu.rename", "Zmień nazwę..." },
            { "menu.hide", "Ukryj" },
            { "menu.delete", "Usuń" },

            { "selector.title", "Wybierz obszar" },
            { "selector.hint", "Kliknij i przeciągnij, aby zaznaczyć obszar. Esc anuluje." },

            { "profile.label", "Profil" },
            { "profile.new", "Nowy profil" },
            { "profile.newPrompt", "Nazwa nowego profilu" },
            { "profile.delete", "Usuń ten profil" },
            { "profile.deleteConfirm", "Usunąć profil „{0}” i wszystkie jego regiony?" },
            { "region.deleteConfirm", "Usunąć „{0}”?" },

            { "help.noSource.tibia", "Nie znaleziono Tibii.\n\nUruchom grę i zaloguj się. Jeśli grasz na pełnym ekranie, użyj silnika graficznego DirectX 11 lub DirectX 12: OpenGL używa wyłącznego pełnego ekranu, którego Windows nie potrafi odbić.\n\nNie masz gry? Wybierz „Okno ćwiczebne” jako źródło, aby wypróbować program." },
            { "help.noSource.obs", "Nie znaleziono projektora OBS.\n\nW OBS Studio kliknij prawym przyciskiem podgląd (albo scenę lub źródło) i wybierz „Projektor w oknie”. Zostaw to okno otwarte; może być na innym monitorze albo za innymi oknami, ale nie zminimalizowane." },
            { "help.noSource.minimized", "Okno źródłowe jest zminimalizowane.\n\nWindows nie potrafi odbić zminimalizowanego okna. Przywróć je i spróbuj ponownie." },

            { "selector.hintMany", "Kliknij i przeciągnij, aby zaznaczyć obszar. Przytrzymaj Shift, aby zaznaczyć kilka. Esc anuluje." },
            { "selector.progress", "Zaznaczono: {0}. Shift+przeciągnij kolejny, R powtarza odstęp, Enter kończy." },
            { "status.placedMany", "Lustra dodane jako grupa. Przytrzymaj Shift, przeciągając jedno, aby przesunąć grupę." },
            { "status.timerAdded", "Dodano minutnik. Ustaw czas i skrót, przeciągnij go na miejsce i zablokuj." },
            { "status.timerLimit", "Profil mieści do {0} minutników." },
            { "status.hotkeyTaken", "Windows odrzucił ten skrót (już zajęty?): {0}" },
            { "status.badDuration", "Wpisz czas, na przykład 10 (minut), 10:30 albo 1:00:00." },
            { "action.addTimer", "Dodaj minutnik odnowienia" },

            { "timer.ready", "Gotowe" },
            { "timer.start", "Start / od nowa" },
            { "timer.reset", "Wyzeruj" },
            { "timer.duration", "Czas..." },
            { "timer.hotkey", "Skrót..." },
            { "timer.sound", "Dźwięk po zakończeniu" },
            { "timer.startTip", "Uruchom ten minutnik albo zacznij od nowa" },
            { "timer.durationTip", "Zmień czas" },
            { "timer.hotkeyTip", "Wybierz klawisz, który uruchamia ten minutnik" },
            { "timer.defaultName", "Minutnik {0}" },
            { "timer.durationPrompt", "Czas (minuty albo minuty:sekundy)" },

            { "hotkey.title", "Skrót minutnika" },
            { "hotkey.explain", "Naciśnij kombinację klawiszy, która ma uruchamiać ten minutnik.\n\nGdy Echo HUD działa, Windows przekazuje tę kombinację do Echo HUD, a NIE do gry. Wybierz taką, której nie używasz w grze, na przykład Ctrl+F5, i naciśnij ją zaraz po użyciu przedmiotu." },
            { "hotkey.none", "Brak skrótu" },
            { "hotkey.clear", "Usuń" },

            { "settings.title", "Ustawienia" },
            { "settings.onlyWithGame", "Pokazuj lustra tylko, gdy gra jest na wierzchu" },
            { "settings.snap", "Przyciągaj lustra do siebie" },
            { "settings.tray", "Minimalizuj do obszaru powiadomień" },
            { "settings.autostart", "Uruchamiaj razem z Windows" },
            { "settings.lockHotkey", "Skrót blokowania / odblokowania: {0}" },
            { "settings.showHotkey", "Skrót pokazywania / ukrywania: {0}" },
            { "settings.lockHotkeyTitle", "Skrót blokowania / odblokowania" },
            { "settings.showHotkeyTitle", "Skrót pokazywania / ukrywania" },
            { "settings.language", "Język" },
            { "settings.languageAuto", "Taki jak w Windows" },
            { "status.languageNext", "Język zmieni się przy następnym uruchomieniu Echo HUD." },
            { "hotkey.explainGlobal", "Naciśnij kombinację klawiszy, której chcesz używać.\n\nGdy Echo HUD działa, Windows przekazuje tę kombinację do Echo HUD, a NIE do gry. Wybierz taką, której nie używasz w grze, na przykład Ctrl+F6." },

            { "profile.import", "Importuj profil..." },
            { "profile.export", "Eksportuj ten profil..." },
            { "profile.fileType", "Profil Echo HUD" },
            { "status.exported", "Profil wyeksportowany. Nie zawiera położenia plików dźwiękowych." },
            { "status.imported", "Zaimportowano jako „{0}”." },
            { "action.releases", "Sprawdź nową wersję (otwiera stronę)" },

            { "tray.open", "Otwórz Echo HUD" },
            { "tray.lock", "Zablokuj lustra" },
            { "tray.unlock", "Odblokuj lustra" },
            { "tray.exit", "Zakończ" },
            { "tray.hint", "Echo HUD nadal działa. Kliknij tę ikonę, aby otworzyć go ponownie." },

            { "menu.group", "Grupa" },
            { "group.none", "Bez grupy" },
            { "group.new", "Nowa grupa..." },
            { "group.newPrompt", "Nazwa nowej grupy" },
            { "group.defaultName", "Grupa {0}" },
            { "status.badGroup", "Nazwa grupy musi mieć od 1 do 40 znaków." },
            { "selector.noRoom", "W tym kierunku nie ma już miejsca na kolejny obszar." },

            { "timer.minutes", "{0} min" },
            { "timer.durationOther", "Inny..." },
            { "timer.shape", "Kształt" },
            { "timer.shapeCircle", "Koło" },
            { "timer.shapeBar", "Pasek" },
            { "timer.colour", "Kolor" },
            { "timer.soundOff", "Bez dźwięku" },
            { "timer.soundStandard", "Dźwięk standardowy" },
            { "timer.soundFile", "Wybierz plik dźwiękowy (.wav)..." },
            { "timer.countUp", "Licz dalej po zakończeniu" },

            { "about.text", "Echo HUD {0}\nWolne oprogramowanie na licencji GPL-3.0.\n\nCo robi\nPokazuje na żywo kopie fragmentów okna w małych pływających okienkach, korzystając z miniatur Windows (tych samych, co w podglądach Alt+Tab).\n\nCzego nigdy nie robi\n- nie czyta ani nie zmienia pamięci ani plików gry\n- nie wstrzykuje kodu, nie instaluje hooków ani sterowników\n- nie wysyła klawiszy ani kliknięć do gry\n- nie przechwytuje ani nie analizuje pikseli ekranu\n- nie łączy się z internetem\n\nNie jest powiązany z CipSoft ani BattlEye i nie został przez nie zatwierdzony. Ich zasady mogą się zmienić; z każdego narzędzia firm trzecich korzystasz na własne ryzyko." }
        };
    }
}
