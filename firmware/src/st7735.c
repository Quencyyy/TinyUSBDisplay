#include "st7735.h"
#include <string.h>

#define CMD_SWRESET 0x01u
#define CMD_SLPOUT  0x11u
#define CMD_COLMOD  0x3Au
#define CMD_MADCTL  0x36u
#define CMD_CASET   0x2Au
#define CMD_RASET   0x2Bu
#define CMD_RAMWR   0x2Cu
#define CMD_DISPON  0x29u

static bool bus_valid(const st7735_bus_t *bus) {
    return bus && bus->set_cs && bus->set_dc && bus->set_reset &&
           bus->write && bus->delay_ms;
}

static void command(st7735_t *lcd, uint8_t value) {
    lcd->bus.set_cs(false);
    lcd->bus.set_dc(false);
    lcd->bus.write(&value, 1);
    lcd->bus.set_cs(true);
}

static void data(st7735_t *lcd, const uint8_t *values, size_t length) {
    lcd->bus.set_cs(false);
    lcd->bus.set_dc(true);
    lcd->bus.write(values, length);
    lcd->bus.set_cs(true);
}

bool st7735_init(st7735_t *lcd, const st7735_bus_t *bus,
                 uint8_t column_offset, uint8_t row_offset) {
    const uint8_t rgb565 = 0x05u;
    const uint8_t madctl = 0x00u;
    if (!lcd || !bus_valid(bus)) return false;
    memset(lcd, 0, sizeof *lcd);
    lcd->bus = *bus;
    lcd->column_offset = column_offset;
    lcd->row_offset = row_offset;
    lcd->bus.set_cs(true);
    lcd->bus.set_reset(false); lcd->bus.delay_ms(20);
    lcd->bus.set_reset(true); lcd->bus.delay_ms(120);
    command(lcd, CMD_SWRESET); lcd->bus.delay_ms(150);
    command(lcd, CMD_SLPOUT); lcd->bus.delay_ms(120);
    command(lcd, CMD_COLMOD); data(lcd, &rgb565, 1);
    command(lcd, CMD_MADCTL); data(lcd, &madctl, 1);
    command(lcd, CMD_DISPON); lcd->bus.delay_ms(20);
    return true;
}

static void set_window(st7735_t *lcd, uint8_t x, uint8_t y,
                       uint8_t width, uint8_t height) {
    uint16_t x0 = (uint16_t)x + lcd->column_offset;
    uint16_t y0 = (uint16_t)y + lcd->row_offset;
    uint16_t x1 = x0 + width - 1u;
    uint16_t y1 = y0 + height - 1u;
    uint8_t bounds[4];
    bounds[0] = (uint8_t)(x0 >> 8); bounds[1] = (uint8_t)x0;
    bounds[2] = (uint8_t)(x1 >> 8); bounds[3] = (uint8_t)x1;
    command(lcd, CMD_CASET); data(lcd, bounds, sizeof bounds);
    bounds[0] = (uint8_t)(y0 >> 8); bounds[1] = (uint8_t)y0;
    bounds[2] = (uint8_t)(y1 >> 8); bounds[3] = (uint8_t)y1;
    command(lcd, CMD_RASET); data(lcd, bounds, sizeof bounds);
    command(lcd, CMD_RAMWR);
}

bool st7735_write_block(st7735_t *lcd, uint8_t x, uint8_t y,
                        uint8_t width, uint8_t height,
                        const uint8_t *rgb565_be, size_t length) {
    size_t expected = (size_t)width * height * 2u;
    if (!lcd || !rgb565_be || width == 0 || height == 0 ||
        (uint16_t)x + width > ST7735_WIDTH ||
        (uint16_t)y + height > ST7735_HEIGHT || length != expected) return false;
    set_window(lcd, x, y, width, height);
    data(lcd, rgb565_be, length);
    return true;
}

bool st7735_clear(st7735_t *lcd, uint16_t rgb565) {
    uint8_t row[ST7735_WIDTH * 2u];
    uint16_t i;
    for (i = 0; i < ST7735_WIDTH; ++i) {
        row[i * 2u] = (uint8_t)(rgb565 >> 8);
        row[i * 2u + 1u] = (uint8_t)rgb565;
    }
    for (i = 0; i < ST7735_HEIGHT; ++i) {
        if (!st7735_write_block(lcd, 0, (uint8_t)i, ST7735_WIDTH, 1,
                                row, sizeof row)) return false;
    }
    return true;
}
