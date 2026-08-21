#ifndef MINIDISPLAY_ST7735_H
#define MINIDISPLAY_ST7735_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ST7735_WIDTH 80u
#define ST7735_HEIGHT 160u

typedef struct {
    void (*set_cs)(bool high);
    void (*set_dc)(bool data);
    void (*set_reset)(bool high);
    void (*write)(const uint8_t *data, size_t length);
    void (*delay_ms)(uint32_t milliseconds);
} st7735_bus_t;

typedef struct {
    st7735_bus_t bus;
    uint8_t column_offset;
    uint8_t row_offset;
} st7735_t;

bool st7735_init(st7735_t *lcd, const st7735_bus_t *bus,
                 uint8_t column_offset, uint8_t row_offset);
bool st7735_write_block(st7735_t *lcd, uint8_t x, uint8_t y,
                        uint8_t width, uint8_t height,
                        const uint8_t *rgb565_be, size_t length);
bool st7735_clear(st7735_t *lcd, uint16_t rgb565);

#endif
