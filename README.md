# Net Core GDI Image Mask Benchmarks

The purpose of this repository is to test the fastest possible way to apply a transparency
mask in a GDI Image.For this context it is assumed we have an image without the 
Alpha channel and an image of the same dimensions consisting of only the alpha channel.
The algorithms should create a new GDI Bitmap with the transparency mask applied. 

## Images used
The images generated are simple. A single block or Red colour and the transparency mask is
a gradient that fades from upper left corner (0) to lower right corner (255). Every image
generated is saved on disk to verify the algorithms are producing the right result.