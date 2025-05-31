// -------------------------------------------------------------------------
// separable 1D Gaussian (row or column) using __local memory
// -------------------------------------------------------------------------
__kernel void print_id(
    __global const uchar *inputBuffer,   // RGBA or RGB interleaved
    __global       uchar *outputBuffer,  // RGBA or RGB interleaved
    const int          width,
    const int          height,
    const int          channels,         // e.g. 3 or 4
    const int          kernelSize,       // odd:  3,5,7,...
    const int          is_horizontal,    // 1 = run row‐wise, 0 = run column‐wise
    __global const float *gausKernelBuffer, // 1D array of length = kernelSize
    __local       uchar *lineBuffer      // will be bound to (lineLength*channels) bytes
) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    int radius = kernelSize / 2;

    // Compute output index = (y * width + x) * channels
    int outIdx = (y * width + x) * channels;

    // Figure out how long “the line” is: width (row) or height (column)
    int lineLength = (is_horizontal == 1) ? width : height;

    // -------------------------------------------------------
    // (A) Copy the entire row or entire column into local memory
    // -------------------------------------------------------
    if (is_horizontal == 1) {
        // Row‐pass: copy row y into lineBuffer[0..(width-1)], each pixel = 'channels' bytes
        for (int i = 0; i < width; i++) {
            int imgIdx = (y * width + i) * channels; // pixel (i, y)
            int bufIdx = i * channels;               // slot i in lineBuffer
            lineBuffer[bufIdx + 0] = inputBuffer[imgIdx + 0];
            lineBuffer[bufIdx + 1] = inputBuffer[imgIdx + 1];
            lineBuffer[bufIdx + 2] = inputBuffer[imgIdx + 2];
            if (channels == 4) {
                lineBuffer[bufIdx + 3] = inputBuffer[imgIdx + 3];
            }
        }
    } else {
        // Column‐pass: copy column x into lineBuffer[0..(height-1)]
        for (int i = 0; i < height; i++) {
            int imgIdx = (i * width + x) * channels; // pixel (x, i)
            int bufIdx = i * channels;               // slot i in lineBuffer
            lineBuffer[bufIdx + 0] = inputBuffer[imgIdx + 0];
            lineBuffer[bufIdx + 1] = inputBuffer[imgIdx + 1];
            lineBuffer[bufIdx + 2] = inputBuffer[imgIdx + 2];
            if (channels == 4) {
                lineBuffer[bufIdx + 3] = inputBuffer[imgIdx + 3];
            }
        }
    }

    // -------------------------------------------------------
    // (B) Barrier to ensure the entire work‐group has populated lineBuffer
    // -------------------------------------------------------
    barrier(CLK_LOCAL_MEM_FENCE);

    // -------------------------------------------------------
    // (C) Perform 1D convolution (only one axis at a time)
    // -------------------------------------------------------
    float sumCh0 = 0.0f, sumCh1 = 0.0f, sumCh2 = 0.0f, sumCh3 = 0.0f;
    for (int k = -radius; k <= radius; k++) {
        // *** THE ONLY CHANGE HERE compared to the previous version: ***
        // We now read directly from the 1D array gausKernelBuffer[k + radius].
        // (Before, we had radius*kernelSize + (k+radius), which was wrong.)
        float w = gausKernelBuffer[k + radius];

        int idx;
        if (is_horizontal == 1) {
            // Horizontal: slide along x
            int cx = x + k;
            cx = clamp(cx, 0, width - 1);
            idx = (cx * channels);
        } else {
            // Vertical: slide along y
            int cy = y + k;
            cy = clamp(cy, 0, height - 1);
            idx = (cy * channels);
        }

        sumCh0 += ((float)lineBuffer[idx + 0]) * w;
        sumCh1 += ((float)lineBuffer[idx + 1]) * w;
        sumCh2 += ((float)lineBuffer[idx + 2]) * w;
        if (channels == 4) {
            sumCh3 += ((float)lineBuffer[idx + 3]) * w;
        }
    }

    // -------------------------------------------------------
    // (D) Write back the clamped result into outputBuffer
    // -------------------------------------------------------
    outputBuffer[outIdx + 0] = (uchar)clamp(sumCh0, 0.0f, 255.0f);
    outputBuffer[outIdx + 1] = (uchar)clamp(sumCh1, 0.0f, 255.0f);
    outputBuffer[outIdx + 2] = (uchar)clamp(sumCh2, 0.0f, 255.0f);
    if (channels == 4) {
        // Preserve alpha from the original input
        outputBuffer[outIdx + 3] = inputBuffer[outIdx + 3];
    }
}