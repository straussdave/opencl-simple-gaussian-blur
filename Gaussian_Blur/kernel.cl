__kernel void print_id(
		__global const uchar *inputBuffer,
		__global uchar *outputBuffer,
		const int width,
		const int height,
		const int channels)
{
	if(get_global_id(0) == 0 && get_global_id(1) == 0)
	printf("OCL: from kernel\n");

	size_t x = get_global_id(0);
	size_t y = get_global_id(1);

	size_t index = (y * width + x) * channels;

	outputBuffer[index] = inputBuffer[index];	
	outputBuffer[index+1] = inputBuffer[index+1];
	outputBuffer[index+2] = inputBuffer[index+2];
	if(channels == 4)
		outputBuffer[index+3] = inputBuffer[index+3];

	
}