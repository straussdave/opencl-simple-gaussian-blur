





__kernel void print_id(
		__global const uchar *inputBuffer,
		__global uchar *outputBuffer,
		const int width,
		const int height,
		const int channels,
		const int kernelSize,
		const int is_horizontal,
		__global const float *gausKernelBuffer,
    	__local uchar *lineBuffer)
{
	size_t x = get_global_id(0);
	size_t y = get_global_id(1);
	int range = kernelSize / 2;
	float sum_r = 0.0f;
	float sum_g = 0.0f;
	float sum_b = 0.0f;

	size_t index = (y * width + x) * channels;

	int lineLength = (is_horizontal == 1) ? width : height;

	if (is_horizontal == 1) {
		for (int i = 0; i < width; i++) {
			int imgIndex  = (y * width + i) * channels;  // (i, y)
			int lineIndex = i * channels;

			lineBuffer[lineIndex + 0] = inputBuffer[imgIndex + 0]; // R
			lineBuffer[lineIndex + 1] = inputBuffer[imgIndex + 1]; // G
			lineBuffer[lineIndex + 2] = inputBuffer[imgIndex + 2]; // B
			if(channels == 4) {
				lineBuffer[lineIndex + 3] = inputBuffer[imgIndex + 3];
			}
		}
	} else {
		for (int i = 0; i < height; i++) {
			int imgIndex  = (i * width + x) * channels;  // (x, i)
			int lineIndex = i * channels;

			lineBuffer[lineIndex + 0] = inputBuffer[imgIndex + 0]; // R
			lineBuffer[lineIndex + 1] = inputBuffer[imgIndex + 1]; // G
			lineBuffer[lineIndex + 2] = inputBuffer[imgIndex + 2]; // B
			if(channels == 4) {
				lineBuffer[lineIndex + 3] = inputBuffer[imgIndex + 3];
			}
		}
	}

	barrier(CLK_LOCAL_MEM_FENCE);

	int current_x = 0;
    for (int kernel_x = -range; kernel_x <= range; kernel_x++) {
		if (is_horizontal == 1){
			current_x = x + kernel_x;
		} 
		else{
			current_x = y + kernel_x;
		}
			

		current_x = clamp(current_x, 0, lineLength - 1);

        int neighborIndex = current_x * channels;
        int kernelIndex = kernel_x + range;
        float weight = gausKernelBuffer[kernelIndex];

        sum_r += lineBuffer[neighborIndex]     * weight;
        sum_g += lineBuffer[neighborIndex + 1] * weight;
        sum_b += lineBuffer[neighborIndex + 2] * weight;
	}

	outputBuffer[index] = (uchar)clamp(sum_r, 0.0f, 255.0f);	
	outputBuffer[index+1] = (uchar)clamp(sum_g, 0.0f, 255.0f);
	outputBuffer[index+2] = (uchar)clamp(sum_b, 0.0f, 255.0f);
	if(channels == 4) {
		outputBuffer[index+3] = inputBuffer[index+3];
	}	
}