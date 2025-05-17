/*
* a kernel that add the elements of two vectors pairwise
*
*			__kernel void vector_add(
*				__global const int *A,
*				__global const int *B,
*				__global int *C)
*			{
*				size_t i = get_global_id(0);
*				C[i] = A[i] + B[i];
*			}
*/


__kernel void print_id(
		__global const uchar *inputBuffer,
		__global uchar *outputBuffer,
		const int width,
		const int height,
		const int channels)
{
	if(get_global_id(0) == 0 && get_global_id(1) == 0)
	printf("OCL: from kernel");

	size_t x = get_global_id(0);
	size_t y = get_global_id(1);

	outputBuffer[x*y] = inputBuffer[x*y];	
	outputBuffer[x*y+1] = inputBuffer[x*y+1];
	outputBuffer[x*y+2] = inputBuffer[x*y+2];
	if(channels == 4)
		outputBuffer[x*y+3] = inputBuffer[x*y+3];

	
}