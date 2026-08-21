cmake_minimum_required(VERSION 3.16)
project(minidisplay_protocol C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

add_library(minidisplay_protocol
    src/protocol.c
    src/protocol_stream.c
    src/st7735.c
    src/display_service.c)
target_include_directories(minidisplay_protocol PUBLIC include)

add_executable(protocol_test tests/protocol_test.c)
target_link_libraries(protocol_test PRIVATE minidisplay_protocol)

add_executable(display_service_test tests/display_service_test.c)
target_link_libraries(display_service_test PRIVATE minidisplay_protocol)

add_executable(protocol_stream_test tests/protocol_stream_test.c)
target_link_libraries(protocol_stream_test PRIVATE minidisplay_protocol)

enable_testing()
add_test(NAME protocol_test COMMAND protocol_test)
add_test(NAME display_service_test COMMAND display_service_test)
add_test(NAME protocol_stream_test COMMAND protocol_stream_test)
