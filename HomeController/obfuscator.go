package main

import (
	"encoding/base64"
	"strconv"
	"strings"
	"time"
)

const XORConstant = 0x36

func ToBase64(input string) string {
	return base64.StdEncoding.EncodeToString([]byte(input))
}

func FromBase64(input string) string {
	data, _ := base64.StdEncoding.DecodeString(input)
	return string(data)
}

func XOREncrypt(input string) string {
	data := []byte(input)
	for i := 0; i < len(data); i++ {
		data[i] = data[i] ^ XORConstant
	}
	return string(data)
}

func XORDecrypt(input string) string {
	data := []byte(input)
	for i := 0; i < len(data); i++ {
		data[i] = data[i] ^ XORConstant
	}
	return string(data)
}

func Encrypt(input string) string {
	input = ToBase64(input)
	input += "," + strconv.FormatInt(time.Now().Unix(), 10)
	input = ToBase64(input)
	input = XOREncrypt(input) // Perhaps go to AES
	return ToBase64(input)
}

func Decrypt(input string) Content {
	input = FromBase64(input)
	input = XORDecrypt(input)
	input = FromBase64(input)

	decryptedInput := strings.Split(input, ",")

	dateTimeInt64, _ := strconv.ParseInt(decryptedInput[1], 10, 64)
	parsedContent := Content{
		ContentMessage: FromBase64(decryptedInput[0]),
		DateTime:       time.Unix(dateTimeInt64, 0),
	}

	return parsedContent
}

type Content struct {
	ContentMessage string
	DateTime       time.Time
}

func (content Content) IsReplay() bool {
	return (time.Now().UTC().Sub(content.DateTime).Seconds() > 10)
}
