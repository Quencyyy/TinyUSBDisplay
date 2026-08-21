#include "protocol_stream.h"
#include <stdio.h>
#include <string.h>

#define CHECK(x) do { if (!(x)) { printf("FAIL line %d: %s\n", __LINE__, #x); return 1; } } while (0)

typedef struct { unsigned packets; unsigned errors; uint16_t payload_length; } state_t;
static void on_packet(const md_packet_t *packet, void *context) {
    state_t *state = (state_t *)context;
    ++state->packets;
    state->payload_length = packet->payload_length;
}
static void on_error(md_result_t error, void *context) {
    state_t *state = (state_t *)context;
    (void)error;
    ++state->errors;
}

int main(void) {
    uint8_t pixels[640];
    uint8_t encoded[MD_MAX_PACKET_SIZE];
    md_packet_t packet = { MD_CMD_PIXEL_BLOCK, 1, 1, 0, 0, 80, 4, 640, 0, pixels };
    md_stream_t stream;
    state_t state = {0};
    size_t written = 0, offset;
    memset(pixels, 0xaa, sizeof pixels);
    CHECK(md_encode(&packet, encoded, sizeof encoded, &written) == MD_OK);
    CHECK(written == 658u);
    md_stream_init(&stream, on_packet, on_error, &state);
    for (offset = 0; offset < written; offset += 64) {
        size_t chunk = written - offset;
        if (chunk > 64) chunk = 64;
        md_stream_feed(&stream, encoded + offset, chunk);
    }
    CHECK(state.packets == 1 && state.errors == 0 && state.payload_length == 640);
    md_stream_feed(&stream, (const uint8_t *)"noise", 5);
    encoded[14] ^= 1u;
    md_stream_feed(&stream, encoded, written);
    CHECK(state.packets == 1 && state.errors == 1);
    puts("protocol_stream_test: PASS");
    return 0;
}
