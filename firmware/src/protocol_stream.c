#include "protocol_stream.h"

static uint16_t read_u16_le(const uint8_t *p) {
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

void md_stream_init(md_stream_t *stream, md_packet_callback_t on_packet,
                    md_error_callback_t on_error, void *context) {
    if (!stream) return;
    stream->used = 0;
    stream->expected = 0;
    stream->on_packet = on_packet;
    stream->on_error = on_error;
    stream->context = context;
}

void md_stream_reset(md_stream_t *stream) {
    if (!stream) return;
    stream->used = 0;
    stream->expected = 0;
}

static void report_error(md_stream_t *stream, md_result_t error) {
    if (stream->on_error) stream->on_error(error, stream->context);
}

static void consume_byte(md_stream_t *stream, uint8_t value) {
    md_packet_t packet;
    size_t consumed;
    md_result_t result;
    if (stream->used == 0 && value != MD_MAGIC_0) return;
    if (stream->used == 1 && value != MD_MAGIC_1) {
        stream->used = value == MD_MAGIC_0 ? 1u : 0u;
        stream->buffer[0] = value;
        return;
    }
    if (stream->used >= sizeof stream->buffer) {
        report_error(stream, MD_ERR_LENGTH);
        md_stream_reset(stream);
        return;
    }
    stream->buffer[stream->used++] = value;
    if (stream->used == MD_HEADER_SIZE) {
        uint16_t payload_length = read_u16_le(stream->buffer + 10);
        if (payload_length > MD_MAX_BLOCK_PAYLOAD) {
            report_error(stream, MD_ERR_LENGTH);
            md_stream_reset(stream);
            return;
        }
        stream->expected = md_encoded_size(payload_length);
    }
    if (stream->expected && stream->used == stream->expected) {
        result = md_decode(stream->buffer, stream->used, &packet, &consumed);
        if (result == MD_OK) {
            if (stream->on_packet) stream->on_packet(&packet, stream->context);
        } else {
            report_error(stream, result);
        }
        md_stream_reset(stream);
    }
}

void md_stream_feed(md_stream_t *stream, const uint8_t *data, size_t length) {
    size_t i;
    if (!stream || (!data && length)) return;
    for (i = 0; i < length; ++i) consume_byte(stream, data[i]);
}
