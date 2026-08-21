#include "display_service.h"
#include <stdio.h>
#include <string.h>

#define CHECK(x) do { if (!(x)) { printf("FAIL line %d: %s\n", __LINE__, #x); return 1; } } while (0)

static size_t bytes_written;
static uint8_t last_command;
static bool dc_state;
static void set_cs(bool high) { (void)high; }
static void set_dc(bool data) { dc_state = data; }
static void set_reset(bool high) { (void)high; }
static void delay_ms(uint32_t ms) { (void)ms; }
static void write_bus(const uint8_t *data, size_t length) {
    bytes_written += length;
    if (!dc_state && length == 1) last_command = data[0];
}

int main(void) {
    st7735_bus_t bus = { set_cs, set_dc, set_reset, write_bus, delay_ms };
    st7735_t lcd;
    display_service_t service;
    uint8_t pixels[640];
    md_packet_t packet = { MD_CMD_FRAME_BEGIN, 3, 1, 0, 0, 0, 0, 0, 0, NULL };
    display_reply_t result;
    memset(pixels, 0x1f, sizeof pixels);
    CHECK(st7735_init(&lcd, &bus, 24, 0));
    display_service_init(&service, &lcd);
    result = display_service_handle(&service, &packet);
    CHECK(result.kind == DISPLAY_REPLY_ACK && service.frame_open);
    packet.command = MD_CMD_PIXEL_BLOCK; packet.width = 80; packet.height = 4;
    packet.payload_length = sizeof pixels; packet.payload = pixels;
    result = display_service_handle(&service, &packet);
    CHECK(result.kind == DISPLAY_REPLY_ACK && service.next_sequence == 1);
    CHECK(last_command == 0x2c && bytes_written >= sizeof pixels);
    result = display_service_handle(&service, &packet);
    CHECK(result.kind == DISPLAY_REPLY_NACK);
    packet.command = MD_CMD_FRAME_END; packet.payload = NULL; packet.payload_length = 0;
    result = display_service_handle(&service, &packet);
    CHECK(result.kind == DISPLAY_REPLY_ACK && !service.frame_open);
    puts("display_service_test: PASS");
    return 0;
}
