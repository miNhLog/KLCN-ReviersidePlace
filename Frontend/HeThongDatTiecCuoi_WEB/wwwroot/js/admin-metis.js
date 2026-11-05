(() => {
    const storageKey = "adminColorMode";
    const root = document.documentElement;
    const themeButton = document.getElementById("adminThemeToggle");
    const fullscreenButton = document.getElementById("adminFullscreenToggle");

    if (!themeButton || !fullscreenButton) return;

    const currentTheme = () => root.dataset.adminTheme === "dark" ? "dark" : "light";

    const syncThemeButton = () => {
        const dark = currentTheme() === "dark";
        themeButton.setAttribute("aria-label", dark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
        themeButton.title = dark ? "Giao diện sáng" : "Giao diện tối";
        const icon = themeButton.querySelector("i");
        icon?.classList.toggle("bi-moon", !dark);
        icon?.classList.toggle("bi-sun", dark);
    };

    const syncFullscreenButton = () => {
        const active = Boolean(document.fullscreenElement);
        fullscreenButton.setAttribute("aria-label", active ? "Thoát chế độ toàn màn hình" : "Bật chế độ toàn màn hình");
        fullscreenButton.title = active ? "Thoát toàn màn hình" : "Toàn màn hình";
        const icon = fullscreenButton.querySelector("i");
        icon?.classList.toggle("bi-arrows-fullscreen", !active);
        icon?.classList.toggle("bi-fullscreen-exit", active);
    };

    themeButton.addEventListener("click", () => {
        const nextTheme = currentTheme() === "dark" ? "light" : "dark";
        if (nextTheme === "dark") root.dataset.adminTheme = "dark";
        else delete root.dataset.adminTheme;

        try {
            localStorage.setItem(storageKey, nextTheme);
        } catch (error) {
            // Chế độ giao diện vẫn hoạt động trong phiên hiện tại.
        }

        syncThemeButton();
        window.dispatchEvent(new CustomEvent("admin-theme-change", { detail: { theme: nextTheme } }));
    });

    fullscreenButton.addEventListener("click", async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen();
            else await document.documentElement.requestFullscreen();
        } catch (error) {
            fullscreenButton.classList.add("is-unavailable");
            window.setTimeout(() => fullscreenButton.classList.remove("is-unavailable"), 800);
        }
    });

    document.addEventListener("fullscreenchange", syncFullscreenButton);
    document.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            document.getElementById("backofficeSearch")?.focus();
        }
    });
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", event => {
        try {
            if (localStorage.getItem(storageKey)) return;
        } catch (error) {
            return;
        }

        root.dataset.adminTheme = event.matches ? "dark" : "light";
        syncThemeButton();
        window.dispatchEvent(new CustomEvent("admin-theme-change", {
            detail: { theme: currentTheme() }
        }));
    });
    syncThemeButton();
    syncFullscreenButton();
})();
