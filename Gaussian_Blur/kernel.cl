__kernel void print_id(
		__global const uchar *inputBuffer,
		__global uchar *outputBuffer,
		const int width,
		const int height,
		const int channels,
		const int kernelSize,
		__global const float *gausKernelBuffer)
{
	size_t x = get_global_id(0);
	size_t y = get_global_id(1);
	int range = kernelSize / 2;
	float sum_r = 0.0f;
	float sum_g = 0.0f;
	float sum_b = 0.0f;

	size_t index = (y * width + x) * channels;

    for (int kernel_x = -range; kernel_x <= range; kernel_x++) {
        int current_x = x + kernel_x;
        int current_y = y;

		current_x = clamp(current_x, 0, width - 1);

		//because kernel and image are 1D arrays, we need to calculate the index from the different x and y
        int neighborIndex = (current_y * width + current_x) * channels;
        int kernelIndex = kernel_x + range;
        float weight = gausKernelBuffer[kernelIndex];

        sum_r += inputBuffer[neighborIndex]     * weight;
        sum_g += inputBuffer[neighborIndex + 1] * weight;
        sum_b += inputBuffer[neighborIndex + 2] * weight;
	}

	outputBuffer[index] = (uchar)clamp(sum_r, 0.0f, 255.0f);	
	outputBuffer[index+1] = (uchar)clamp(sum_g, 0.0f, 255.0f);
	outputBuffer[index+2] = (uchar)clamp(sum_b, 0.0f, 255.0f);;
	if(channels == 4)
		outputBuffer[index+3] = inputBuffer[index+3];

}