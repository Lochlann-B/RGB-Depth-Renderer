using System.Runtime.CompilerServices;
using FFmpeg.AutoGen;

namespace VideoHandler;

using FFMpegCore;

public unsafe class VideoStreamHandler
{
    private AVFormatContext* pFormatContext = null;
    private AVCodecContext* pCodecContext = null;
    private AVFrame* pFrame = null;
    private int videoStreamIndex = -1;
    private AVBufferRef* hwDeviceCtx = null;

    public void Initialize(string filePath)
    {
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
        AVHWDeviceType hwType = ffmpeg.av_hwdevice_find_type_by_name("cuda");
        AVBufferRef* hwDeviceCtxx;
        if (ffmpeg.av_hwdevice_ctx_create(&hwDeviceCtxx, hwType, null, null, 0) < 0)
            throw new ApplicationException("Could not initialize the hardware device.");

        pCodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(hwDeviceCtx);
        AVCodec* codec = ffmpeg.avcodec_find_decoder_by_name("h264_cuvid");
        if (codec == null) throw new ApplicationException("Codec not found.");

        if (ffmpeg.avcodec_open2(pCodecContext, codec, null) < 0)
            throw new ApplicationException("Could not open the codec.");

        hwDeviceCtx = hwDeviceCtxx;
        
        pFrame = ffmpeg.av_frame_alloc();
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
                        return pFrame;  // Frame is ready and can be used
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