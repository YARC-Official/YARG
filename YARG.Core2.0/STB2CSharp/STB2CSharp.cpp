#include "STB2CSharp.h"

unsigned char* load_image_from_memory(const unsigned char* bytes, int length, int* width, int* height, int* components)
{
    return stbi_load_from_memory(bytes, length, width, height, components, 0);
}

void free_image(unsigned char* image)
{
    stbi_image_free(image);
}