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
		__global const byte *inputBuffer,
		__global byte *outputBuffer)
{

	size_t x = get_global_id(0);
	size_t y = get_global_id(1);

	outputBuffer[x, y] = inputBuffer[x, y];

	
}