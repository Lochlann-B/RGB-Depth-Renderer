using System.Runtime.CompilerServices;
using FFmpeg.AutoGen;
using AVBufferRef = FFmpeg.AutoGen.Abstractions.AVBufferRef;
using AVCodec = FFmpeg.AutoGen.Abstractions.AVCodec;
using AVCodecContext = FFmpeg.AutoGen.Abstractions.AVCodecContext;
using AVFormatContext = FFmpeg.AutoGen.Abstractions.AVFormatContext;
using AVFrame = FFmpeg.AutoGen.Abstractions.AVFrame;
using AVMediaType = FFmpeg.AutoGen.Abstractions.AVMediaType;
using AVPacket = FFmpeg.AutoGen.Abstractions.AVPacket;
using AVPixelFormat = FFmpeg.AutoGen.Abstractions.AVPixelFormat;
using DynamicallyLoadedBindings = FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings;
using ffmpeg = FFmpeg.AutoGen.Abstractions.ffmpeg;
using SwsContext = FFmpeg.AutoGen.Abstractions.SwsContext;

// using FFmpeg.AutoGen;

namespace VideoHandler;


public unsafe class VideoStreamHandler
{
    private AVFormatContext* pFormatContext = null;
    private AVCodecContext* pCodecContext = null;
    private AVFrame* pFrame = null;
    private AVFrame* pFrameRGB = null;
    private SwsContext* swsCtx = null;
    private int videoStreamIndex = -1;
    private AVBufferRef* hwDeviceCtx = null;

    public static void Init()
    {
        Console.WriteLine("Current directory: " + Environment.CurrentDirectory);
        Console.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
        FFmpegBinariesHelper.RegisterFFmpegBinaries();
        DynamicallyLoadedBindings.Initialize();
        Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
    }

    public void Initialize(string filePath)
    {
        Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
        ffmpeg.avdevice_register_all();
        ffmpeg.avformat_network_init();
        
        AVFormatContext* pFormatContextt = null;

        pFormatContextt = ffmpeg.avformat_alloc_context();
        ffmpeg.avformat_open_input(&pFormatContextt, filePath, null, null);
        ffmpeg.avformat_find_stream_info(pFormatContextt, null);

        pFormatContext = pFormatContextt;
        
        // Find the video stream
        videoStreamIndex = -1;
        for (int i = 0; i < pFormatContextt->nb_streams; i++)
        {
            if (pFormatContextt->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                videoStreamIndex = i;
                break;
            }
        }

        if (videoStreamIndex == -1) throw new ApplicationException("Video stream not found.");

        pCodecContext = ffmpeg.avcodec_alloc_context3(null);
        ffmpeg.avcodec_parameters_to_context(pCodecContext, pFormatContextt->streams[videoStreamIndex]->codecpar);

        // Set up hardware acceleration
        // AVHWDeviceType hwType = ffmpeg.av_hwdevice_find_type_by_name("cuda");
        // AVBufferRef* hwDeviceCtxx;
        // if (ffmpeg.av_hwdevice_ctx_create(&hwDeviceCtxx, hwType, null, null, 0) < 0)
        //     throw new ApplicationException("Could not initialize the hardware device.");

        // pCodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(hwDeviceCtxx);
        // AVCodec* codec = ffmpeg.avcodec_find_decoder_by_name("h264");
        
        // AVCodec* codec = ffmpeg.avcodec_find_decoder_by_name("h264_cuvid");
        AVCodec* codec = ffmpeg.avcodec_find_decoder(pCodecContext->codec_id);
        if (codec == null) throw new ApplicationException("Codec not found.");

        if (ffmpeg.avcodec_open2(pCodecContext, codec, null) < 0)
            throw new ApplicationException("Could not open the codec.");

        // hwDeviceCtx = hwDeviceCtxx;
        
        pFrame = ffmpeg.av_frame_alloc();
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_DEBUG);
        
        // Allocate the output frame
        pFrameRGB = ffmpeg.av_frame_alloc();
    
        // Specify the pixel format and dimensions for the output frame
        pFrameRGB->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;
        int width = pCodecContext->width;
        int height = pCodecContext->height;
        pFrameRGB->width = width;
        pFrameRGB->height = height;
    
        // Allocate memory for the data of the output frame
        ffmpeg.av_frame_get_buffer(pFrameRGB, 0);

        // Initialize SwsContext for the conversion
        swsCtx = ffmpeg.sws_getContext(width, height, AVPixelFormat.AV_PIX_FMT_YUV420P,
            width, height, AVPixelFormat.AV_PIX_FMT_RGB24,
            ffmpeg.SWS_BILINEAR, null, null, null);

    }

    public AVFrame* GetNextFrame()
    {
        AVPacket packet;
        while (ffmpeg.av_read_frame(pFormatContext, &packet) >= 0)
        {
            if (packet.stream_index == videoStreamIndex)
            {
                if (ffmpeg.avcodec_send_packet(pCodecContext, &packet) == 0)
                {
                    if (ffmpeg.avcodec_receive_frame(pCodecContext, pFrame) == 0)
                    {
                        ffmpeg.av_packet_unref(&packet);
                        ffmpeg.sws_scale(swsCtx, pFrame->data, pFrame->linesize, 0, pCodecContext->height, pFrameRGB->data, pFrameRGB->linesize);
                        int dataLineSize = pFrameRGB->linesize[0];  // Line size of the RGB data buffer
                        byte* data = pFrameRGB->data[0];            // Pointer to the data buffer
                        
                        Console.WriteLine("First few RGB pixels:");
                        for (int i = 0; i < 10; i++)  // Print the first 10 pixels' RGB values
                        {
                            if (i * 3 + 2 < dataLineSize)  // Ensure we do not read out of bounds
                            {
                                Console.WriteLine($"Pixel {i}: R={data[i * 3]}, G={data[i * 3 + 1]}, B={data[i * 3 + 2]}");
                            }
                        }
                        return pFrameRGB;
                    }
                }
            }
            ffmpeg.av_packet_unref(&packet);
        }
        return null;  // No more frames or an error occurred
    }

    public void Cleanup()
    {
        var pFramePtr = pFrame;
        ffmpeg.av_frame_free(&pFramePtr);

        var pCodecContextPtr = pCodecContext;
        ffmpeg.avcodec_close(pCodecContextPtr);

        var hwDeviceCtxPtr = hwDeviceCtx;
        ffmpeg.av_buffer_unref(&hwDeviceCtxPtr);

        var pFormatContextPtr = pFormatContext;
        ffmpeg.avformat_close_input(&pFormatContextPtr);
    }
}