#include "protocol.h"
#include <stdio.h>
#include <string.h>

#define CHECK(x) do { if (!(x)) { printf("FAIL line %d: %s\n", __LINE__, #x); return 1; } } while (0)

int main(void) {
    uint8_t pixels[160];
    uint8_t encoded[MD_MAX_PACKET_SIZE];
    md_packet_t source = { MD_CMD_PIXEL_BLOCK, 7, 1, 0, 42, 80, 1, 160, 12, pixels };
    md_packet_t decoded;
    size_t written = 0, consumed = 0;
    size_t i;

    CHECK(md_crc16_ccitt_false((const uint8_t *)"123456789", 9) == 0x29B1u);
    for (i = 0; i < sizeof pixels; i += 2) { pixels[i] = 0xF8; pixels[i + 1] = 0x00; }
    CHECK(md_encode(&source, encoded, sizeof encoded, &written) == MD_OK);
    CHECK(written == 178u);
    CHECK(md_decode(encoded, written, &decoded, &consumed) == MD_OK);
    CHECK(consumed == written);
    CHECK(decoded.command == MD_CMD_PIXEL_BLOCK && decoded.y == 42);
    CHECK(decoded.payload_length == sizeof pixels);
    CHECK(memcmp(decoded.payload, pixels, sizeof pixels) == 0);

    encoded[16] ^= 1u;
    CHECK(md_decode(encoded, written, &decoded, &consumed) == MD_ERR_PAYLOAD_CRC);
    encoded[16] ^= 1u;
    encoded[3] ^= 1u;
    CHECK(md_decode(encoded, written, &decoded, &consumed) == MD_ERR_HEADER_CRC);

    puts("protocol_test: PASS");
    return 0;
}
