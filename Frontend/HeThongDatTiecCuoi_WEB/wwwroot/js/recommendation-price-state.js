(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) module.exports = api;
    if (root) root.RecommendationPriceState = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    'use strict';

    function number(value) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function normalizeDish(raw) {
        return {
            dishId: number(raw && (raw.dishId ?? raw.DishId)),
            dishCode: String(raw && (raw.dishCode || raw.DishCode) || ''),
            dishName: String(raw && (raw.dishName || raw.DishName) || 'Món ăn'),
            category: String(raw && (raw.category || raw.Category) || 'Món khác'),
            price: number(raw && (raw.price ?? raw.Price))
        };
    }

    function validateCustomDishIds(items) {
        const source = Array.isArray(items) ? items : [];
        const ids = source.map(item => number(item && item.dishId));
        const valid = ids.length > 0 &&
            ids.every(id => Number.isInteger(id) && id > 0) &&
            new Set(ids).size === ids.length;
        return { valid, ids };
    }

    function calculateCustomPrice(state) {
        if (!state) return 0;
        const items = Array.isArray(state.items) ? state.items : [];
        const originals = Array.isArray(state.originalItems) ? state.originalItems : [];
        const same = items.length === originals.length &&
            items.every((item, index) => number(item.dishId) === number(originals[index] && originals[index].dishId));
        if (same) return number(state.originalPricePerTable);
        const originalTotal = originals.reduce((sum, item) => sum + number(item.price), 0);
        const selectedTotal = items.reduce((sum, item) => sum + number(item.price), 0);
        return Math.max(0, number(state.originalPricePerTable) + selectedTotal - originalTotal);
    }

    function applyOfficialQuote(combo, quote) {
        if (!combo || !quote) return false;
        if (number(quote.officialHallPrice) >= 0) combo.hallPriceNum = number(quote.officialHallPrice);
        if (number(quote.officialBaseMenuPricePerTable) > 0)
            combo.menuPriceNum = number(quote.officialBaseMenuPricePerTable);
        if (number(quote.officialMenuPricePerTable) > 0) {
            if (combo.customMenu) {
                combo.customMenu.customPricePerTable = number(quote.officialMenuPricePerTable);
                if (Array.isArray(quote.officialSelectedDishes)) {
                    const oldById = new Map((combo.customMenu.items || []).map(item => [number(item.dishId), item]));
                    combo.customMenu.items = quote.officialSelectedDishes.map(raw => {
                        const current = normalizeDish(raw);
                        return { ...(oldById.get(current.dishId) || {}), ...current };
                    });
                }
            } else {
                combo.menuPriceNum = number(quote.officialMenuPricePerTable);
            }
        }
        if (number(quote.officialDecorationPrice) >= 0)
            combo.decorPriceNum = number(quote.officialDecorationPrice);
        if (Array.isArray(quote.officialServices)) {
            combo.services = quote.officialServices.map(service => ({
                serviceCode: service.serviceCode || '',
                serviceName: service.serviceName || '',
                price: number(service.price),
                note: service.note || ''
            }));
        }
        combo.servicesTotalNum = number(quote.officialServicesTotal);
        combo.totalCostNum = number(quote.officialEstimatedTotal);
        return true;
    }

    function refreshCustomizer(combo, menuData, allDishesRaw) {
        if (!combo || !menuData)
            return { ok: false, code: 'MENU_UNAVAILABLE', message: 'Không tải được thực đơn gốc mới nhất.' };
        const originalItems = (menuData.dishes || menuData.Dishes || []).map(normalizeDish);
        const allDishes = (allDishesRaw || []).map(normalizeDish);
        const basePrice = number(menuData.pricePerTable ?? menuData.PricePerTable);
        if (basePrice <= 0 || originalItems.length === 0)
            return { ok: false, code: 'BASE_MENU_INVALID', message: 'Dữ liệu giá hoặc danh sách món gốc không hợp lệ.' };

        let selectedItems;
        if (combo.customMenu) {
            const validation = validateCustomDishIds(combo.customMenu.items);
            if (!validation.valid)
                return { ok: false, code: 'DISH_IDS_INVALID', message: 'Danh sách món tùy chỉnh thiếu mã hợp lệ hoặc có món trùng.' };
            const currentById = new Map(allDishes.map(item => [item.dishId, item]));
            const unavailable = validation.ids.filter(id => !currentById.has(id));
            if (unavailable.length > 0)
                return { ok: false, code: 'DISH_UNAVAILABLE', unavailableDishIds: unavailable, message: 'Có món đã chọn không còn phục vụ.' };
            selectedItems = validation.ids.map(id => ({ ...currentById.get(id) }));
        } else {
            selectedItems = originalItems.map(item => ({ ...item }));
        }

        combo.menuName = menuData.menuName || menuData.MenuName || combo.menuName;
        combo.menuPriceNum = basePrice;
        const state = {
            comboIndex: combo.comboIndex,
            originalName: combo.menuName,
            originalPricePerTable: basePrice,
            originalItems: originalItems.map(item => ({ ...item })),
            items: selectedItems.map(item => ({ ...item })),
            allDishes: allDishes.map(item => ({ ...item }))
        };
        if (combo.customMenu) {
            combo.customMenu.originalName = combo.menuName;
            combo.customMenu.items = selectedItems.map(item => ({ ...item }));
            combo.customMenu.customPricePerTable = calculateCustomPrice(state);
        }
        return { ok: true, state, price: combo.customMenu ? combo.customMenu.customPricePerTable : basePrice };
    }

    return { validateCustomDishIds, calculateCustomPrice, applyOfficialQuote, refreshCustomizer };
});
