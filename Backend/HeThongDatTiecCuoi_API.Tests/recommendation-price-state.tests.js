'use strict';
const assert = require('node:assert/strict');
const path = require('node:path');
const stateApi = require(path.resolve(__dirname,
    '../../Frontend/HeThongDatTiecCuoi_WEB/wwwroot/js/recommendation-price-state.js'));

const combo = {
    comboIndex: 0,
    menuName: 'Menu cũ',
    menuPriceNum: 4000000,
    hallPriceNum: 20000000,
    decorPriceNum: 18000000,
    services: [],
    customMenu: {
        customName: 'Menu tùy chỉnh',
        customPricePerTable: 4300000,
        items: [{ dishId: 1, dishName: 'Món 1', price: 1000000 }, { dishId: 4, dishName: 'Món 4', price: 1600000 }]
    }
};

stateApi.applyOfficialQuote(combo, {
    officialHallPrice: 22000000,
    officialBaseMenuPricePerTable: 4200000,
    officialMenuPricePerTable: 4700000,
    officialDecorationPrice: 19000000,
    officialServicesTotal: 3000000,
    officialEstimatedTotal: 147400000,
    officialSelectedDishes: [{ dishId: 1, dishName: 'Món 1', price: 1100000 }, { dishId: 4, dishName: 'Món 4', price: 1800000 }],
    officialServices: [{ serviceCode: 'DV002', serviceName: 'Đón khách', price: 3000000, note: '' }]
});
assert.equal(combo.menuPriceNum, 4200000);
assert.equal(combo.customMenu.customPricePerTable, 4700000);
assert.equal(combo.customMenu.items[1].price, 1800000);
assert.equal(combo.hallPriceNum, 22000000);

const refreshed = stateApi.refreshCustomizer(combo, {
    menuName: 'Menu mới',
    pricePerTable: 4200000,
    dishes: [{ dishId: 1, dishName: 'Món 1', price: 1100000 }, { dishId: 2, dishName: 'Món 2', price: 1400000 }]
}, [
    { dishId: 1, dishName: 'Món 1', price: 1100000 },
    { dishId: 2, dishName: 'Món 2', price: 1400000 },
    { dishId: 4, dishName: 'Món 4', price: 1800000 },
    { dishId: 5, dishName: 'Món 5', price: 2000000 }
]);
assert.equal(refreshed.ok, true);
assert.equal(refreshed.state.originalPricePerTable, 4200000);
assert.equal(refreshed.state.items[1].price, 1800000);
assert.equal(refreshed.price, 4600000); // 4.2m + (1.1m + 1.8m) - (1.1m + 1.4m)

refreshed.state.items[1] = refreshed.state.allDishes.find(item => item.dishId === 5);
assert.equal(stateApi.calculateCustomPrice(refreshed.state), 4800000);

const unavailableCombo = JSON.parse(JSON.stringify(combo));
const unavailable = stateApi.refreshCustomizer(unavailableCombo, {
    menuName: 'Menu mới', pricePerTable: 4200000,
    dishes: [{ dishId: 1, dishName: 'Món 1', price: 1100000 }, { dishId: 2, dishName: 'Món 2', price: 1400000 }]
}, [{ dishId: 1, dishName: 'Món 1', price: 1100000 }]);
assert.equal(unavailable.ok, false);
assert.equal(unavailable.code, 'DISH_UNAVAILABLE');
assert.deepEqual(unavailableCombo.customMenu.items, combo.customMenu.items);

assert.equal(stateApi.validateCustomDishIds([{ dishId: 1 }, { dishId: 0 }]).valid, false);
assert.equal(stateApi.validateCustomDishIds([{ dishId: 1 }, { dishId: 1 }]).valid, false);
console.log('recommendation-price-state: all behavioral tests passed');
