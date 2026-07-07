using System;

namespace YARG.Song.Exporters
{
    /// <summary>
    /// The self-contained HTML template for the web song browser export. Split at
    /// static init into four segments around the three substitution markers in
    /// document order: /*DATA*/, /*GENRES*/, /*META*/. Consumers stream
    /// Seg0 -> DATA -> Seg1 -> GENRES -> Seg2 -> META -> Seg3 to a file so the full
    /// HTML document is never materialized in memory. The template text uses LF
    /// newlines and no BOM; its bytes are emitted verbatim into the exported page,
    /// so do not let an editor reformat the interior of the string literal.
    /// </summary>
    public static class WebBrowserTemplate
    {
        private const string MARKER_DATA = "/*DATA*/";
        private const string MARKER_GENRES = "/*GENRES*/";
        private const string MARKER_META = "/*META*/";

        // Verbatim template (quote chars doubled per C# verbatim-string syntax).
        private const string TEMPLATE = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>YARG Song Library</title>
    <style>
        :root {
            --background: #0a0a0a;
            --surface: #1a1a1a;
            --text: #e0e0e0;
            --subtle-text: #888;
            --border: #333;
            --accent: #00DDFB;

            --badge-v: #79D304;
            --badge-g: #FF1D23;
            --badge-d: #FFE900;
            --badge-k: #00BFFF;
            --badge-b: #FF8400;
            --tier-orange: #FF8400;
            --tier-red: #F32B37;
        }

        [data-theme=""light""] {
            --background: #ffffff;
            --surface: #f5f5f5;
            --text: #1a1a1a;
            --subtle-text: #666;
            --border: #ddd;
            --accent: #00DDFB;

            --badge-v: #79D304;
            --badge-g: #FF1D23;
            --badge-d: #C9A800;
            --badge-k: #00BFFF;
            --badge-b: #FF8400;
            --tier-orange: #FF8400;
            --tier-red: #F32B37;
        }

        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, system-ui, ""Segoe UI"", Roboto, sans-serif;
            background: var(--background);
            color: var(--text);
            overflow: hidden;
            min-height: 100vh;
            height: 100vh;
            display: flex;
            flex-direction: column;
        }

        header {
            position: relative;
            top: 0;
            z-index: 100;
            background: var(--surface);
            padding: 1rem;
            border-bottom: 1px solid var(--border);
            flex: 0 0 auto;
        }

        h1 {
            font-size: 1.5rem;
            margin-bottom: 0.5rem;
            color: var(--text);
        }

        .counts {
            display: flex;
            gap: 1rem;
            margin-bottom: 0.75rem;
            font-size: 0.875rem;
            color: var(--subtle-text);
        }

        #search {
            width: 100%;
            padding: 0.5rem;
            background: var(--background);
            border: 1px solid var(--border);
            color: var(--text);
            border-radius: 4px;
            font-size: 1rem;
        }

        .search-row:focus-within {
            outline: 2px solid var(--accent);
            outline-offset: 2px;
        }

        .filters {
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            margin-bottom: 0.75rem;
        }

        .chip {
            padding: 0.375rem 0.75rem;
            background: var(--background);
            border: 1px solid var(--border);
            color: var(--text);
            border-radius: 20px;
            cursor: pointer;
            font-size: 0.875rem;
            transition: all 0.2s;
        }

        .chip:hover {
            background: var(--surface);
        }

        .chip.active {
            background: var(--accent);
            color: #000;
            border-color: var(--accent);
        }

        #theme-toggle {
            position: absolute;
            top: 1rem;
            right: 1rem;
            background: #444;
            border: 1px solid #555;
            color: #aaa;
            border-radius: 50%;
            width: 36px;
            height: 36px;
            padding: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
        }

        #theme-toggle:hover {
            background: #555;
        }

        [data-theme=""light""] #theme-toggle {
            background: #e0e0e0;
            border-color: #ccc;
            color: #b8b8b8;
        }

        [data-theme=""light""] #theme-toggle:hover {
            background: #d4d4d4;
        }

        /* Dark mode (default): show sun (switch TO light). Light mode: show moon (switch TO dark). */
        .icon-sun { display: inline; }
        .icon-moon { display: none; }
        [data-theme=""light""] .icon-sun { display: none; }
        [data-theme=""light""] .icon-moon { display: inline; }

        #scroll {
            flex: 1 1 auto;
            min-height: 0;
            overflow-y: auto;
            position: relative;
        }

        #spacer {
            position: relative;
            width: 100%;
        }

        .row {
            display: grid;
            grid-template-columns: 1fr 1.5fr 62px 44px 1fr 110px;
            gap: 0.5rem;
            padding: 0.75rem;
            border-bottom: 1px solid var(--border);
            position: absolute;
            width: 100%;
            left: 0;
            height: 40px;
            align-items: center;
            cursor: pointer;
        }

        .row:hover {
            background: var(--surface);
        }

        .cell {
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            font-size: 0.875rem;
            color: var(--text);
        }

        .cell-album, .cell-year {
            color: var(--subtle-text);
        }

        .cell-length {
            text-align: right;
            font-variant-numeric: tabular-nums;
            padding-right: 1ch;
        }

        .col-header {
            display: grid;
            grid-template-columns: 1fr 1.5fr 62px 44px 1fr 110px;
            gap: 0.5rem;
            padding: 0.5rem 0.75rem;
            border-bottom: 2px solid var(--border);
            background: var(--surface);
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            color: var(--subtle-text);
            flex: 0 0 auto;
        }

        .col-header-cell {
            cursor: pointer;
            user-select: none;
            display: flex;
            align-items: center;
            gap: 0.25rem;
        }

        .col-header-cell:hover { color: var(--text); }
        .col-header-cell.active { color: var(--accent); }
        .sort-indicator { font-size: 0.6rem; }

        .badge {
            display: inline-block;
            width: 1.5em;
            text-align: center;
            padding: 0.125rem 0;
            border-radius: 3px;
            font-size: 0.75rem;
            font-weight: 600;
            margin-right: 0.25rem;
            color: #000;
        }

        .badge-v { background: var(--badge-v); }
        .badge-g { background: var(--badge-g); }
        .badge-d { background: var(--badge-d); }
        .badge-k { background: var(--badge-k); }
        .badge-b { background: var(--badge-b); }

        .badge-empty {
            background: var(--border);
            color: var(--subtle-text);
            opacity: 0.5;
        }

        .chip.chip-v.active { background: var(--badge-v); color: #000; border-color: var(--badge-v); }
        .chip.chip-g.active { background: var(--badge-g); color: #000; border-color: var(--badge-g); }
        .chip.chip-d.active { background: var(--badge-d); color: #000; border-color: var(--badge-d); }
        .chip.chip-k.active { background: var(--badge-k); color: #000; border-color: var(--badge-k); }
        .chip.chip-b.active { background: var(--badge-b); color: #000; border-color: var(--badge-b); }

        /* Bottom sheet overlay */
        #sheet-backdrop {
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.5);
            z-index: 200;
            opacity: 0;
            visibility: hidden;
            transition: opacity 0.3s, visibility 0.3s;
        }

        #sheet-backdrop.open {
            opacity: 1;
            visibility: visible;
        }

        #sheet {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            background: var(--surface);
            border-top: 1px solid var(--border);
            z-index: 201;
            max-height: 70vh;
            overflow-y: auto;
            transform: translateY(100%);
            transition: transform 0.3s ease-out;
            padding: 1.5rem;
        }

        #sheet.open {
            transform: translateY(0);
        }

        .sheet-header {
            font-size: 1.25rem;
            font-weight: 600;
            margin-bottom: 1rem;
            color: var(--text);
        }

        .sheet-row {
            display: flex;
            justify-content: space-between;
            margin-bottom: 0.75rem;
            font-size: 0.9rem;
        }

        .sheet-label {
            color: var(--subtle-text);
            font-weight: 500;
        }

        .sheet-value {
            color: var(--text);
            text-align: right;
        }

        .sheet-diff-list {
            margin-top: 1.5rem;
        }

        .sheet-diff-title {
            font-size: 1rem;
            font-weight: 600;
            margin-bottom: 0.75rem;
            color: var(--text);
        }

        .sheet-diff-row {
            display: flex;
            justify-content: space-between;
            padding: 0.5rem 0;
            border-bottom: 1px solid var(--border);
        }

        .sheet-diff-part {
            display: flex;
            align-items: center;
            gap: 0.5rem;
        }

        .sheet-diff-name {
            color: var(--text);
            font-weight: 500;
        }

        .sheet-diff-tier {
            color: var(--subtle-text);
        }

        footer {
            flex: 0 0 auto;
            padding: 0.5rem 1rem;
            font-size: 0.75rem;
            color: var(--subtle-text);
            text-align: center;
            border-top: 1px solid var(--border);
        }

        .search-row {
            display: flex;
            align-items: stretch;
            margin-bottom: 0.75rem;
            background: var(--background);
            border: 1px solid var(--border);
            border-radius: 4px;
        }
        .search-input-wrap {
            flex: 1;
            position: relative;
            display: flex;
            align-items: center;
        }
        #search {
            width: 100%;
            padding: 0.5rem;
            background: transparent;
            border: none;
            color: var(--text);
            font-size: 1rem;
            outline: none;
        }
        .search-divider {
            width: 1px;
            background: var(--border);
            flex-shrink: 0;
        }
        .search-gear {
            position: relative;
            display: flex;
            align-items: center;
            justify-content: center;
            width: 36px;
            cursor: pointer;
            color: var(--subtle-text);
            flex-shrink: 0;
        }
        .search-gear:hover { background: var(--surface); color: var(--text); }
        .search-row > :first-child { border-radius: 4px 0 0 4px; }
        .search-row > :last-child { border-radius: 0 4px 4px 0; }
        .search-popover {
            display: none;
            position: absolute;
            top: 100%;
            right: 0;
            margin-top: 4px;
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 6px;
            padding: 0.5rem 0.75rem;
            z-index: 150;
            white-space: nowrap;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        }
        .search-popover.open { display: block; }
        .search-popover label { display: flex; align-items: center; gap: 0.4rem; padding: 0.2rem 0; font-size: 0.8rem; color: var(--text); cursor: pointer; }
        .search-popover label input { accent-color: var(--accent); }

        .chip-short { display: none; }

        @media (max-width: 900px) {
            .row, .col-header { grid-template-columns: 1fr 1.5fr 62px 44px 1fr; }
            .cell-instruments { display: none; }
        }
        @media (max-width: 720px) {
            .row, .col-header { grid-template-columns: 1fr 1.5fr 62px 44px; }
            .cell-album { display: none; }
            .chip-long { display: none; }
            .chip-short { display: inline; }
        }
        @media (max-width: 540px) {
            .row, .col-header { grid-template-columns: 1fr 1.5fr 62px; }
            .cell-year { display: none; }
        }
    </style>
</head>
<body>
    <header>
        <h1>YARG Song Library</h1>
        <button id=""theme-toggle"">
            <svg class=""icon-moon"" width=""18"" height=""18"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z""/></svg>
            <svg class=""icon-sun"" width=""18"" height=""18"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><circle cx=""12"" cy=""12"" r=""5""/><line x1=""12"" y1=""1"" x2=""12"" y2=""3""/><line x1=""12"" y1=""21"" x2=""12"" y2=""23""/><line x1=""4.22"" y1=""4.22"" x2=""5.64"" y2=""5.64""/><line x1=""18.36"" y1=""18.36"" x2=""19.78"" y2=""19.78""/><line x1=""1"" y1=""12"" x2=""3"" y2=""12""/><line x1=""21"" y1=""12"" x2=""23"" y2=""12""/><line x1=""4.22"" y1=""19.78"" x2=""5.64"" y2=""18.36""/><line x1=""18.36"" y1=""5.64"" x2=""19.78"" y2=""4.22""/></svg>
        </button>
        <div class=""counts"">
            <span id=""total-count"">Total: 0</span>
            <span id=""count"">Showing: 0</span>
        </div>
        <div class=""search-row"">
            <div class=""search-input-wrap"">
                <input type=""search"" id=""search"" placeholder=""Search songs..."">
            </div>
            <div class=""search-divider""></div>
            <div class=""search-gear"" id=""search-gear"">
                <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><circle cx=""12"" cy=""12"" r=""3""/><path d=""M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z""/></svg>
                <div class=""search-popover"" id=""search-popover"">
                    <label><input type=""checkbox"" data-field=""artist"" checked> Artist</label>
                    <label><input type=""checkbox"" data-field=""title"" checked> Title</label>
                    <label><input type=""checkbox"" data-field=""album""> Album</label>
                </div>
            </div>
        </div>
        <div class=""filters"">
            <button class=""chip chip-v"" data-part=""V"">🎤 <span class=""chip-long""><strong>V</strong>ocals / Harmonies</span><span class=""chip-short""><strong>V</strong></span></button>
            <button class=""chip chip-g"" data-part=""G"">🎸 <span class=""chip-long""><strong>G</strong>uitar</span><span class=""chip-short""><strong>G</strong></span></button>
            <button class=""chip chip-d"" data-part=""D"">🥁 <span class=""chip-long""><strong>D</strong>rums</span><span class=""chip-short""><strong>D</strong></span></button>
            <button class=""chip chip-k"" data-part=""K"">🎹 <span class=""chip-long""><strong>K</strong>eys</span><span class=""chip-short""><strong>K</strong></span></button>
            <button class=""chip chip-b"" data-part=""B"">🎸 <span class=""chip-long""><strong>B</strong>ass</span><span class=""chip-short""><strong>B</strong></span></button>
        </div>
    </header>

    <div class=""col-header"">
        <div class=""col-header-cell"" data-sort=""a"">Artist <span class=""sort-indicator""></span></div>
        <div class=""col-header-cell"" data-sort=""t"">Title <span class=""sort-indicator""></span></div>
        <div class=""col-header-cell"" data-sort=""l"">Length <span class=""sort-indicator""></span></div>
        <div class=""col-header-cell cell-year"" data-sort=""y"">Year <span class=""sort-indicator""></span></div>
        <div class=""col-header-cell cell-album"" data-sort=""al"">Album <span class=""sort-indicator""></span></div>
        <div class=""col-header-cell cell-instruments"" data-sort=""p"">Instruments <span class=""sort-indicator""></span></div>
    </div>

    <div id=""scroll"">
        <div id=""spacer""></div>
    </div>

    <footer id=""footer-meta""></footer>

    <div id=""sheet-backdrop"">
        <div id=""sheet"">
            <div class=""sheet-header"" id=""sheet-title""></div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Artist</span>
                <span class=""sheet-value"" id=""sheet-artist""></span>
            </div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Album</span>
                <span class=""sheet-value"" id=""sheet-album""></span>
            </div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Year</span>
                <span class=""sheet-value"" id=""sheet-year""></span>
            </div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Length</span>
                <span class=""sheet-value"" id=""sheet-length""></span>
            </div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Genre</span>
                <span class=""sheet-value"" id=""sheet-genre""></span>
            </div>
            <div class=""sheet-row"">
                <span class=""sheet-label"">Vocals</span>
                <span class=""sheet-value"" id=""sheet-vocals""></span>
            </div>
            <div class=""sheet-diff-list"">
                <div class=""sheet-diff-title"">Difficulties</div>
                <div id=""sheet-diffs""></div>
            </div>
        </div>
    </div>

    <script>
        // Data substitution markers (raw JSON literals; JSON is a subset of JS)
        const DATA = /*DATA*/;
        const GENRES = /*GENRES*/;
        const META = /*META*/;

        // Theme toggle with persistence
        (function() {
            let currentTheme = 'dark';

            function resolveTheme() {
                try {
                    const stored = localStorage.getItem('theme');
                    if (stored && (stored === 'light' || stored === 'dark')) {
                        return stored;
                    }
                } catch (e) {
                    console.warn('localStorage not available:', e);
                }

                try {
                    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) {
                        return 'light';
                    }
                } catch (e) {
                    console.warn('matchMedia not available:', e);
                }

                return 'dark';
            }

            function applyTheme(theme) {
                currentTheme = theme;
                document.documentElement.dataset.theme = theme;
                try {
                    localStorage.setItem('theme', theme);
                } catch (e) {
                    console.warn('localStorage not available:', e);
                }
            }

            function toggleTheme() {
                const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
                applyTheme(newTheme);
            }

            // Initialize theme on load
            applyTheme(resolveTheme());

            // Set up toggle button
            const themeToggle = document.getElementById('theme-toggle');
            if (themeToggle) {
                themeToggle.addEventListener('click', toggleTheme);
            }
        })();

        // Constants for decoding
        const ROW_H = 40;
        const BUFFER = 6;
        const DIFF_NAMES = [""Warmup"",""Apprentice"",""Solid"",""Moderate"",""Challenging"",""Nightmare"",""Impossible""];
        const PART_ORDER = ['V','G','D','K','B'];
        const PART_LABELS = {V:'Vocals',G:'Guitar',D:'Drums',K:'Keys',B:'Bass'};

        // Precompute haystacks for each record
        DATA.forEach(r => {
            r._hayArtist = r.a.toLowerCase();
            r._hayTitle = r.t.toLowerCase();
            r._hayAlbum = r.al ? r.al.toLowerCase() : '';
        });

        // Filter state
        const state = {
            q: [],
            parts: new Set(),
            searchFields: { artist: true, title: true, album: false },
            sortCol: 'a',
            sortAsc: true
        };

        let filtered = [...DATA];

        // DOM elements
        const searchInput = document.getElementById('search');
        const totalCountEl = document.getElementById('total-count');
        const countEl = document.getElementById('count');
        const scrollEl = document.getElementById('scroll');
        const spacerEl = document.getElementById('spacer');

        // Update total count display
        totalCountEl.textContent = `Total: ${DATA.length}`;

        // Filter logic
        function matches(r) {
            // Build haystack from enabled search fields
            let hay = '';
            if (state.searchFields.artist) hay += r._hayArtist + ' ';
            if (state.searchFields.title) hay += r._hayTitle + ' ';
            if (state.searchFields.album) hay += r._hayAlbum + ' ';
            // Check all search terms
            for (const term of state.q) {
                if (!hay.includes(term)) return false;
            }

            // Check all required parts (case-insensitive: K/k both mean Keys present)
            const partsLower = r.p.toLowerCase();
            for (const part of state.parts) {
                if (!partsLower.includes(part.toLowerCase())) return false;
            }

            return true;
        }

        function sortFiltered() {
            if (!state.sortCol) return;
            const col = state.sortCol;
            const dir = state.sortAsc ? 1 : -1;
            const numericCols = new Set(['l', 'y']);
            if (numericCols.has(col)) {
                filtered.sort((a, b) => ((a[col] || 0) - (b[col] || 0)) * dir);
            } else {
                filtered.sort((a, b) => {
                    const va = (a[col] || '').toLowerCase();
                    const vb = (b[col] || '').toLowerCase();
                    return va < vb ? -dir : va > vb ? dir : 0;
                });
            }
        }

        function updateSortIndicators() {
            document.querySelectorAll('.col-header-cell').forEach(cell => {
                const indicator = cell.querySelector('.sort-indicator');
                if (cell.dataset.sort === state.sortCol) {
                    cell.classList.add('active');
                    indicator.textContent = state.sortAsc ? ' ▲' : ' ▼';
                } else {
                    cell.classList.remove('active');
                    indicator.textContent = '';
                }
            });
        }

        function filter() {
            filtered = DATA.filter(matches);
            sortFiltered();
            countEl.textContent = `Showing: ${filtered.length}`;
            scrollEl.scrollTop = 0;
            render();
        }

        // Debounced search
        let searchTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                const query = e.target.value.trim();
                state.q = query ? query.toLowerCase().split(/\s+/) : [];
                filter();
            }, 150);
        });

        // Part filter chips
        document.querySelectorAll('.chip[data-part]').forEach(chip => {
            chip.addEventListener('click', () => {
                const part = chip.dataset.part;
                if (state.parts.has(part)) {
                    state.parts.delete(part);
                    chip.classList.remove('active');
                } else {
                    state.parts.add(part);
                    chip.classList.add('active');
                }
                filter();
            });
        });

        // Search field toggles (gear popover)
        const searchGear = document.getElementById('search-gear');
        const searchPopover = document.getElementById('search-popover');

        searchGear.addEventListener('click', (e) => {
            if (e.target.closest('.search-popover')) return;
            searchPopover.classList.toggle('open');
        });

        document.addEventListener('click', (e) => {
            if (!searchGear.contains(e.target)) {
                searchPopover.classList.remove('open');
            }
        });

        searchPopover.querySelectorAll('input[type=""checkbox""]').forEach(cb => {
            cb.addEventListener('change', (e) => {
                state.searchFields[e.target.dataset.field] = e.target.checked;
                filter();
            });
        });

        // Column header sorting
        document.querySelectorAll('.col-header-cell[data-sort]').forEach(cell => {
            cell.addEventListener('click', () => {
                const col = cell.dataset.sort;
                if (state.sortCol === col) {
                    state.sortAsc = !state.sortAsc;
                } else {
                    state.sortCol = col;
                    state.sortAsc = true;
                }
                updateSortIndicators();
                filter();
            });
        });

        // Helper functions
        function esc(s) {
            if (!s) return '';
            return String(s)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/""/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function fmtLen(seconds) {
            const m = Math.floor(seconds / 60);
            const s = seconds % 60;
            return `${m}:${s.toString().padStart(2, '0')}`;
        }

        function partsBadges(r) {
            let html = '';
            const partsLower = r.p.toLowerCase();
            for (const part of PART_ORDER) {
                const partLower = part.toLowerCase();
                if (partsLower.includes(partLower)) {
                    // Special: V becomes H when harmonies present (vp >= 2)
                    let display = part;
                    if (part === 'V' && r.vp && r.vp >= 2) {
                        display = 'H';
                    }
                    html += `<span class=""badge badge-${partLower}"">${display}</span>`;
                } else {
                    html += `<span class=""badge badge-empty"">-</span>`;
                }
            }
            return html;
        }

        // Virtualized rendering
        let renderFrame;
        function render() {
            cancelAnimationFrame(renderFrame);
            renderFrame = requestAnimationFrame(() => {
                const scrollTop = scrollEl.scrollTop;
                const clientHeight = scrollEl.clientHeight;

                const start = Math.max(0, Math.floor(scrollTop / ROW_H) - BUFFER);
                const visible = Math.ceil(clientHeight / ROW_H) + 2 * BUFFER;
                const end = Math.min(filtered.length, start + visible);

                spacerEl.style.height = (filtered.length * ROW_H) + 'px';

                let html = '';
                for (let i = start; i < end; i++) {
                    const r = filtered[i];
                    const top = i * ROW_H;
                    const ariaLabel = `${r.a} ${r.t}`;
                    html += `<div class=""row"" style=""top:${top}px"" data-index=""${i}"" tabindex=""0"" role=""button"" aria-label=""${esc(ariaLabel)}"">`;
                    html += `<div class=""cell"">${esc(r.a)}</div>`;
                    html += `<div class=""cell"">${esc(r.t)}</div>`;
                    html += `<div class=""cell cell-length"">${fmtLen(r.l)}</div>`;
                    html += `<div class=""cell cell-year"">${esc(r.y || '')}</div>`;
                    html += `<div class=""cell cell-album"">${esc(r.al || '')}</div>`;
                    html += `<div class=""cell cell-instruments"">${partsBadges(r)}</div>`;
                    html += `</div>`;
                }

                spacerEl.innerHTML = html;

                // Attach click and keyboard handlers for detail sheet
                spacerEl.querySelectorAll('.row').forEach(row => {
                    const handleOpen = () => {
                        const idx = parseInt(row.dataset.index);
                        openSheet(filtered[idx]);
                    };
                    row.addEventListener('click', handleOpen);
                    row.addEventListener('keydown', (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            handleOpen();
                        }
                    });
                });
            });
        }

        // Throttled scroll handler
        let scrollFrame;
        scrollEl.addEventListener('scroll', () => {
            cancelAnimationFrame(scrollFrame);
            scrollFrame = requestAnimationFrame(render);
        });

        // Detail bottom sheet
        const sheetBackdrop = document.getElementById('sheet-backdrop');
        const sheet = document.getElementById('sheet');

        function diffTierHtml(ch) {
            if (ch === '?') {
                return `<span style=""color:var(--subtle-text)"">Unknown</span>`;
            }
            const n = parseInt(ch, 36);
            if (isNaN(n)) {
                return `<span style=""color:var(--subtle-text)"">Unknown</span>`;
            }
            // Color: normal text for 0-4, orange for 5, red for 6+
            let color = 'var(--text)';
            if (n === 5) color = 'var(--tier-orange)';
            else if (n >= 6) color = 'var(--tier-red)';
            // Label: ""N Name"" for tiers 0-6, ""Tier N"" for 7+
            const label = n < DIFF_NAMES.length ? `${n} ${DIFF_NAMES[n]}` : `Tier ${n}`;
            return `<span style=""color:${color};font-weight:600"">${label}</span>`;
        }

        function openSheet(r) {
            document.getElementById('sheet-title').textContent = r.t;
            document.getElementById('sheet-artist').textContent = r.a;
            document.getElementById('sheet-album').textContent = r.al || '—';
            document.getElementById('sheet-year').textContent = r.y || '—';
            document.getElementById('sheet-length').textContent = fmtLen(r.l);
            document.getElementById('sheet-genre').textContent = r.g !== null && r.g !== undefined ? GENRES[r.g] : '—';
            document.getElementById('sheet-vocals').textContent = r.vp >= 2 ? `${r.vp} Harmony Parts` : (r.vp === 1 ? 'Solo Vocals' : 'None');

            // Decode difficulties
            let diffHtml = '';
            for (let i = 0; i < PART_ORDER.length; i++) {
                const part = PART_ORDER[i];
                const ch = r.d ? r.d[i] : '.';

                if (ch === '.') {
                    continue; // Part absent, skip
                }

                const partLabel = PART_LABELS[part];
                const tierHtml = diffTierHtml(ch);

                diffHtml += `<div class=""sheet-diff-row"">`;
                diffHtml += `<div class=""sheet-diff-part"">`;
                diffHtml += `<span class=""badge badge-${part.toLowerCase()}"">${part}</span>`;
                diffHtml += `<span class=""sheet-diff-name"">${partLabel}</span>`;
                diffHtml += `</div>`;
                diffHtml += `<div class=""sheet-diff-tier"">${tierHtml}</div>`;
                diffHtml += `</div>`;
            }

            document.getElementById('sheet-diffs').innerHTML = diffHtml;
            sheetBackdrop.classList.add('open');
            sheet.classList.add('open');
        }

        function closeSheet() {
            sheetBackdrop.classList.remove('open');
            sheet.classList.remove('open');
        }

        sheetBackdrop.addEventListener('click', (e) => {
            if (e.target === sheetBackdrop) {
                closeSheet();
            }
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                closeSheet();
            }
        });

        // Swipe-down gesture to close
        let sheetTouchStart;
        let sheetTouchStartY;
        sheet.addEventListener('touchstart', (e) => {
            sheetTouchStart = true;
            sheetTouchStartY = e.touches[0].clientY;
        }, {passive: true});

        sheet.addEventListener('touchmove', (e) => {
            if (!sheetTouchStart) return;
            const currentY = e.touches[0].clientY;
            const diff = currentY - sheetTouchStartY;
            if (diff > 50) {
                closeSheet();
                sheetTouchStart = false;
            }
        }, {passive: true});

        sheet.addEventListener('touchend', () => {
            sheetTouchStart = false;
        });

        // Populate footer with metadata
        const footerMeta = document.getElementById('footer-meta');
        if (footerMeta && META && META.generated) {
            let footerText = `Generated ${META.generated}`;
            if (META.source) {
                footerText += ` from ${META.source}`;
            }
            footerMeta.textContent = footerText;
        }

        // Initial render
        updateSortIndicators();
        filter();
    </script>
</body>
</html>
";

        public static readonly string Seg0;
        public static readonly string Seg1;
        public static readonly string Seg2;
        public static readonly string Seg3;

        static WebBrowserTemplate()
        {
            int d = TEMPLATE.IndexOf(MARKER_DATA, StringComparison.Ordinal);
            int g = TEMPLATE.IndexOf(MARKER_GENRES, StringComparison.Ordinal);
            int m = TEMPLATE.IndexOf(MARKER_META, StringComparison.Ordinal);

            // Each marker must appear exactly once...
            if (d < 0 || g < 0 || m < 0)
            {
                throw new System.InvalidOperationException(
                    "WebBrowserTemplate: missing substitution marker " +
                    "(DATA=" + d + ", GENRES=" + g + ", META=" + m + ").");
            }
            if (TEMPLATE.IndexOf(MARKER_DATA, d + MARKER_DATA.Length, StringComparison.Ordinal) >= 0 ||
                TEMPLATE.IndexOf(MARKER_GENRES, g + MARKER_GENRES.Length, StringComparison.Ordinal) >= 0 ||
                TEMPLATE.IndexOf(MARKER_META, m + MARKER_META.Length, StringComparison.Ordinal) >= 0)
            {
                throw new System.InvalidOperationException(
                    "WebBrowserTemplate: a substitution marker appears more than once.");
            }
            if (!(d < g && g < m))
            {
                throw new System.InvalidOperationException(
                    "WebBrowserTemplate: markers out of document order (expected DATA < GENRES < META).");
            }

            Seg0 = TEMPLATE.Substring(0, d);
            Seg1 = TEMPLATE.Substring(d + MARKER_DATA.Length, g - (d + MARKER_DATA.Length));
            Seg2 = TEMPLATE.Substring(g + MARKER_GENRES.Length, m - (g + MARKER_GENRES.Length));
            Seg3 = TEMPLATE.Substring(m + MARKER_META.Length);
        }
    }
}
