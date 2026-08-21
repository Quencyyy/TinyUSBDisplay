#ifndef MINIDISPLAY_PROTOCOL_STREAM_H
#define MINIDISPLAY_PROTOCOL_STREAM_H

#include "protocol.h"

typedef void (*md_packet_callback_t)(const md_packet_t *packet, void *context);
typedef void (*md_error_callback_t)(md_result_t error, void *context);

typedef struct {
    uint8_t buffer[MD_MAX_PACKET_SIZE];
    size_t used;
    size_t expected;
    md_packet_callback_t on_packet;
    md_error_callback_t on_error;
    void *context;
} md_stream_t;

void md_stream_init(md_stream_t *stream, md_packet_callback_t on_packet,
                    md_error_callback_t on_error, void *context);
void md_stream_reset(md_stream_t *stream);
void md_stream_feed(md_stream_t *stream, const uint8_t *data, size_t length);

#endif
